-- Fictional load for query tuning (docs/query-tuning.md). NEVER run against a real database.
-- 200 resources x 500 reservations = 100,000 rows spread over ~2 years, with a mix of statuses
-- and some soft-deleted rows so the filtered index has something to filter.
SET NOCOUNT ON;
-- Required to write to a table that has a FILTERED index (sqlcmd defaults QUOTED_IDENTIFIER to OFF).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF EXISTS (SELECT 1 FROM rules.Reservations WHERE Subject = N'Carga de prueba')
BEGIN
    PRINT 'Perf data already present.';
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM rules.Resources WHERE Name LIKE N'Perf Lab %')
BEGIN
    ;WITH n AS (
        SELECT TOP (200) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
        FROM sys.all_objects
    )
    INSERT INTO rules.Resources (Id, Name, Category, Capacity, Building, IsActive)
    SELECT NEWID(), CONCAT(N'Perf Lab ', i), N'Lab', 30, N'Edificio Perf', 1
    FROM n;
END;

;WITH n AS (
    SELECT TOP (500) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS k
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT INTO rules.Reservations
    (Id, ResourceId, RequesterId, RequesterRole, StartUtc, EndUtc, Quantity, Subject, Status,
     StatusReason, CreatedUtc, IsDeleted, DeletedUtc)
SELECT
    NEWID(),
    r.Id,
    CONCAT(N'A', RIGHT(CONCAT('00000', n.k), 5)),
    N'Alumno',
    DATEADD(HOUR, 14 + (n.k % 6), DATEADD(DAY, n.k * 1.5, '2029-01-01')),   -- 14:00..19:00 UTC
    DATEADD(HOUR, 15 + (n.k % 6), DATEADD(DAY, n.k * 1.5, '2029-01-01')),   -- 1-hour slots
    1 + (n.k % 5),
    N'Carga de prueba',
    CASE n.k % 10 WHEN 0 THEN N'Cancelled' WHEN 1 THEN N'Rejected' WHEN 2 THEN N'Pending' ELSE N'Approved' END,
    NULL,
    '2028-12-01',
    CASE WHEN n.k % 25 = 0 THEN 1 ELSE 0 END,
    CASE WHEN n.k % 25 = 0 THEN '2028-12-15' END
FROM rules.Resources r
CROSS JOIN n
WHERE r.Name LIKE N'Perf Lab %';

UPDATE STATISTICS rules.Reservations WITH FULLSCAN;
SELECT COUNT(*) AS Reservations FROM rules.Reservations;
