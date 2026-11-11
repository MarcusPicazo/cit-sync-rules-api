-- Execution-plan comparison for the NoOverlap query (docs/query-tuning.md).
-- Run against the fictional load from seed-reservations.sql. Uses sp_executesql with the same
-- parameter types EF Core sends, so plans match what the API really gets.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @rid uniqueidentifier = (SELECT Id FROM rules.Resources WHERE Name = N'Perf Lab 1');
DECLARE @s datetime2 = '$(WinStart)', @e datetime2 = '$(WinEnd)';
DECLARE @params nvarchar(200) = N'@resourceId uniqueidentifier, @endUtc datetime2, @startUtc datetime2';

DECLARE @A nvarchar(max) = N'/* A: EF query (production) */
SELECT [r].[StartUtc], [r].[EndUtc], [r].[Quantity]
FROM [rules].[Reservations] AS [r]
WHERE [r].[IsDeleted] = CAST(0 AS bit) AND [r].[ResourceId] = @resourceId AND [r].[StartUtc] < @endUtc
  AND [r].[EndUtc] > @startUtc AND [r].[Status] <> N''Cancelled'' AND [r].[Status] IN (N''Approved'', N''Pending'')';

DECLARE @B nvarchar(max) = N'/* B: without repeating the index filter (Status <> Cancelled) */
SELECT [r].[StartUtc], [r].[EndUtc], [r].[Quantity]
FROM [rules].[Reservations] AS [r]
WHERE [r].[IsDeleted] = CAST(0 AS bit) AND [r].[ResourceId] = @resourceId AND [r].[StartUtc] < @endUtc
  AND [r].[EndUtc] > @startUtc AND [r].[Status] IN (N''Approved'', N''Pending'')';

DECLARE @C nvarchar(max) = N'/* C: NON-sargable (function on the column) */
SELECT [r].[StartUtc], [r].[EndUtc], [r].[Quantity]
FROM [rules].[Reservations] AS [r]
WHERE [r].[IsDeleted] = CAST(0 AS bit) AND [r].[ResourceId] = @resourceId
  AND DATEADD(MINUTE, 0, [r].[StartUtc]) < @endUtc AND [r].[EndUtc] > @startUtc
  AND [r].[Status] <> N''Cancelled'' AND [r].[Status] IN (N''Approved'', N''Pending'')';

DECLARE @D nvarchar(max) = N'/* D: A + lower bound on StartUtc (requires a max reservation length, here 24h) */
SELECT [r].[StartUtc], [r].[EndUtc], [r].[Quantity]
FROM [rules].[Reservations] AS [r]
WHERE [r].[IsDeleted] = CAST(0 AS bit) AND [r].[ResourceId] = @resourceId
  AND [r].[StartUtc] < @endUtc AND [r].[StartUtc] > DATEADD(HOUR, -24, @startUtc)
  AND [r].[EndUtc] > @startUtc AND [r].[Status] <> N''Cancelled'' AND [r].[Status] IN (N''Approved'', N''Pending'')';

DECLARE @Scan nvarchar(max) = REPLACE(@A, N'FROM [rules].[Reservations] AS [r]', N'FROM [rules].[Reservations] AS [r] WITH (INDEX(PK_Reservations))');
SET @Scan = REPLACE(@Scan, N'/* A: EF query (production) */', N'/* E: A forced to scan the table (no useful index) */');

DECLARE @q TABLE (Label char(1), Sql nvarchar(max));
INSERT @q VALUES ('A', @A), ('B', @B), ('C', @C), ('D', @D), ('E', @Scan);

DECLARE @label char(1), @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Label, Sql FROM @q;
OPEN c;
FETCH NEXT FROM c INTO @label, @sql;
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT '==================== ' + @label + ' ====================';
    SET STATISTICS IO ON;
    EXEC sp_executesql @sql, @params, @resourceId = @rid, @endUtc = @e, @startUtc = @s;
    SET STATISTICS IO OFF;

    -- Plan operators actually used (from the plan cache of the statement we just ran).
    SELECT TOP (1)
        @label AS Q,
        op.n.value('@PhysicalOp', 'varchar(40)') AS PhysicalOp,
        op.n.value('(.//*:Object/@Index)[1]', 'varchar(80)') AS IndexUsed,
        op.n.value('@EstimateRows', 'float') AS EstRows
    FROM sys.dm_exec_query_stats qs
    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) t
    CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) p
    CROSS APPLY p.query_plan.nodes('//*:RelOp[*:IndexScan or *:TableScan]') op(n)
    WHERE t.text LIKE '%/* ' + @label + ':%' AND t.text NOT LIKE '%dm_exec_query_stats%'
    ORDER BY qs.last_execution_time DESC;

    FETCH NEXT FROM c INTO @label, @sql;
END;
CLOSE c;
DEALLOCATE c;
