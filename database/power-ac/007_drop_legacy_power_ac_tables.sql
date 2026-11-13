-- 007: Remove the old Power & A/C tables (the four-component model). Run only after 006.
-- dbo.PowerAndAC is dropped only when every one of its rows has been copied into dbo.PowerAcPmData.
-- Take a backup first. This cannot be undone.
SET NOCOUNT ON;
GO
DROP TABLE IF EXISTS dbo.PowerAcKpiComponentScore;
DROP TABLE IF EXISTS dbo.PowerAcKpiResult;
DROP TABLE IF EXISTS dbo.PowerAcAchievement;
DROP TABLE IF EXISTS dbo.PowerAcComponentResult;
DROP TABLE IF EXISTS dbo.PowerAcAreaAllocation;
DROP TABLE IF EXISTS dbo.PowerAcGroupUserAssignment;
DROP TABLE IF EXISTS dbo.PowerAcAuditLog;
DROP TABLE IF EXISTS dbo.PowerAcComponentVersion;
DROP TABLE IF EXISTS dbo.PowerAcComponentWeight;
DROP TABLE IF EXISTS dbo.PowerAcCalculationSetting;
DROP TABLE IF EXISTS dbo.PowerAcComponent;
DROP TABLE IF EXISTS dbo.PowerAcEngineerGroup;
GO
IF OBJECT_ID(N'dbo.PowerAndAC', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM dbo.PowerAndAC p
        WHERE NOT EXISTS (SELECT 1 FROM dbo.PowerAcPmData t
                          WHERE t.designation = p.Designation AND t.[year] = CAST(p.[Year] AS varchar(10))
                            AND t.[month] = CHOOSE(p.[Month], 'January','February','March','April','May','June','July','August','September','October','November','December')))
    DROP TABLE dbo.PowerAndAC;
ELSE IF OBJECT_ID(N'dbo.PowerAndAC', N'U') IS NOT NULL
    PRINT 'dbo.PowerAndAC was NOT dropped: some rows are not in dbo.PowerAcPmData. Check them, then drop it manually.';
GO
SELECT name FROM sys.tables WHERE name LIKE 'PowerAc%' OR name = 'PowerAndAC' ORDER BY name;
