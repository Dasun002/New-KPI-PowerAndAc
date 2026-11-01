/*
 Power & AC KPI - read-only data readiness report. Makes no changes.
 Lists, per engineer group, what is still missing before the KPI can be scored.
 Set @Year to the reporting year.
*/
SET NOCOUNT ON;
DECLARE @Year INT = YEAR(SYSUTCDATETIME());

-- 1. Scoring versions and approval state (latest version per component)
SELECT c.Code, v.EffectiveYear, v.EffectiveMonth, v.ScoringMethod, v.TargetValue, v.ScoringApproved, v.ApprovedBy, v.ApprovedAt
FROM dbo.PowerAcComponent c
OUTER APPLY (SELECT TOP (1) * FROM dbo.PowerAcComponentVersion x WHERE x.ComponentId = c.Id
             ORDER BY x.EffectiveYear DESC, x.EffectiveMonth DESC) v
ORDER BY c.SortOrder;

-- 2. Engineer groups: mapping and latest allocation
SELECT g.WorkbookAllocationCell, g.WorkbookLabel, g.Code, g.MappingVerified, g.PmDesignation,
       a.AllocationValue, a.EffectiveYear, a.EffectiveMonth
FROM dbo.PowerAcEngineerGroup g
OUTER APPLY (SELECT TOP (1) * FROM dbo.PowerAcAreaAllocation x WHERE x.EngineerGroupId = g.Id
             ORDER BY x.EffectiveYear DESC, x.EffectiveMonth DESC) a
ORDER BY g.SortOrder;

-- 3. Preventive Maintenance months present / verified per group for @Year
SELECT g.Code, g.PmDesignation,
       COUNT(p.Id) AS months_present,
       SUM(CASE WHEN p.is_verified = 1 THEN 1 ELSE 0 END) AS months_verified,
       SUM(CASE WHEN p.Attended > p.Scheduled THEN 1 ELSE 0 END) AS months_attended_above_scheduled,
       STRING_AGG(CASE WHEN p.is_verified = 0 THEN CAST(p.[Month] AS VARCHAR(2)) END, ',') AS unverified_months
FROM dbo.PowerAcEngineerGroup g
LEFT JOIN dbo.PowerAndAC p ON p.Designation = g.PmDesignation AND p.[Year] = @Year
GROUP BY g.Code, g.PmDesignation, g.SortOrder
ORDER BY g.SortOrder;

-- 4. Safety / Corrective / Availability records per group and status for @Year
SELECT g.Code AS engineer_group, c.Code AS component, r.Status, COUNT(*) AS records,
       STRING_AGG(CAST(r.[Month] AS VARCHAR(2)), ',') AS months
FROM dbo.PowerAcComponentResult r
JOIN dbo.PowerAcEngineerGroup g ON g.Id = r.EngineerGroupId
JOIN dbo.PowerAcComponent c ON c.Id = r.ComponentId
WHERE r.[Year] = @Year
GROUP BY g.Code, c.Code, r.Status, g.SortOrder, c.SortOrder
ORDER BY g.SortOrder, c.SortOrder, r.Status;

-- 5. Saved snapshots per period and status
SELECT [Year], PeriodType, PeriodNumber, Status, COUNT(*) AS groups, MAX(CalculatedAt) AS calculated_at
FROM dbo.PowerAcKpiResult
GROUP BY [Year], PeriodType, PeriodNumber, Status
ORDER BY [Year], PeriodType, PeriodNumber, Status;
