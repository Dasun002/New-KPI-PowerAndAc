/*
 Power & AC KPI - read-only verification queries. Run after 001_power_ac_kpi_deploy.sql.
 Makes no changes.
*/
SET NOCOUNT ON;

-- 1. All nine tables exist
SELECT name AS table_name
FROM sys.tables
WHERE name IN (N'PowerAcComponent', N'PowerAcComponentWeight', N'PowerAcEngineerGroup', N'PowerAcGroupUserAssignment',
               N'PowerAcAreaAllocation', N'PowerAcComponentResult', N'PowerAcCalculationSetting',
               N'PowerAcKpiResult', N'PowerAcKpiComponentScore')
ORDER BY name;  -- expect 9 rows

-- 2. Components and their approval state
SELECT Code, Name, TargetValue, TargetUnit, DataSource, ScoringMethod, ScoringApproved, IsActive FROM dbo.PowerAcComponent ORDER BY SortOrder;

-- 3. Effective weights (expect 25 x 4 = 100 for 2026-01)
SELECT w.EffectiveYear, w.EffectiveMonth, SUM(w.WeightPercent) AS total_weight, COUNT(*) AS components
FROM dbo.PowerAcComponentWeight w GROUP BY w.EffectiveYear, w.EffectiveMonth ORDER BY 1, 2;

-- 4. Settings
SELECT s.*, k.keyPerformanceIndicators, k.pointsApplicable
FROM dbo.PowerAcCalculationSetting s JOIN dbo.KpiDefinition k ON k.id = s.KpiDefinitionId;

-- 5. Engineer group mapping (expect 20 rows; 19 verified, D19 unverified until confirmed)
SELECT g.WorkbookAllocationCell, g.WorkbookLabel, g.Code, r.network_engineer, r.lea_code, g.PmDesignation, g.MappingVerified, g.MappingNote
FROM dbo.PowerAcEngineerGroup g LEFT JOIN dbo.regiondata r ON r.id = g.RegionDataId
ORDER BY g.SortOrder;

-- 6. PowerAndAC rows that no engineer group maps to (should be empty)
SELECT DISTINCT p.Designation
FROM dbo.PowerAndAC p
WHERE NOT EXISTS (SELECT 1 FROM dbo.PowerAcEngineerGroup g WHERE g.PmDesignation = p.Designation);

-- 7. Existing PowerAndAC data unchanged (compare with the count taken before deployment)
SELECT COUNT(*) AS powerandac_rows FROM dbo.PowerAndAC;
