-- Removes the fictional load created by seed-reservations.sql and the "Edificio Perf" rule set
-- used to capture the EF query. Local dev database only.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DELETE FROM rules.Reservations
WHERE Subject = N'Carga de prueba'
   OR ResourceId IN (SELECT Id FROM rules.Resources WHERE Name LIKE N'Perf Lab %');

DELETE FROM rules.Resources WHERE Name LIKE N'Perf Lab %';

DECLARE @perfRuleSets TABLE (Id uniqueidentifier);
INSERT @perfRuleSets SELECT Id FROM rules.RuleSets WHERE Scope = N'Building' AND ScopeKey = N'Edificio Perf';

DELETE FROM rules.AuditEntries
WHERE (Entity = N'RuleSet' AND EntityId IN (SELECT CONVERT(nvarchar(100), Id) FROM @perfRuleSets))
   OR (Entity = N'RuleSetVersion' AND EntityId IN (
        SELECT CONVERT(nvarchar(100), v.Id) FROM rules.RuleSetVersions v WHERE v.RuleSetId IN (SELECT Id FROM @perfRuleSets)));

DELETE FROM rules.RuleSetVersionRules
WHERE RuleSetVersionId IN (SELECT v.Id FROM rules.RuleSetVersions v WHERE v.RuleSetId IN (SELECT Id FROM @perfRuleSets));
DELETE FROM rules.RuleSetVersions WHERE RuleSetId IN (SELECT Id FROM @perfRuleSets);
DELETE FROM rules.RuleSets WHERE Id IN (SELECT Id FROM @perfRuleSets);

SELECT
    (SELECT COUNT(*) FROM rules.Reservations) AS Reservations,
    (SELECT COUNT(*) FROM rules.Resources WHERE Name LIKE N'Perf Lab %') AS PerfResources,
    (SELECT COUNT(*) FROM @perfRuleSets) AS PerfRuleSetsRemoved;
