-- 007: Remove the old Power & A/C tables (the four-component model). Run only after 006.
-- Step 1 checks that every dbo.PowerAndAC row was copied into dbo.PowerAcPmData. If not, the script stops and
-- nothing is dropped. Take a backup first. This cannot be undone.
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.PowerAndAC', N'U') IS NOT NULL
   AND EXISTS (
        SELECT 1 FROM dbo.PowerAndAC p
        WHERE NOT EXISTS (SELECT 1 FROM dbo.PowerAcPmData t
                          WHERE t.designation = p.Designation AND t.[year] = CAST(p.[Year] AS varchar(10))
                            AND t.[month] = CHOOSE(p.[Month], 'January','February','March','April','May','June','July','August','September','October','November','December')))
BEGIN
    THROW 50070, 'Not all dbo.PowerAndAC rows are in dbo.PowerAcPmData. Nothing was dropped. Run 006 again and check the data.', 1;
END
GO
-- Step 2: the old configuration, approval, audit and result tables
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
-- Step 3: the legacy PM table, now that its rows are confirmed copied
DROP TABLE IF EXISTS dbo.PowerAndAC;
GO
SELECT name FROM sys.tables WHERE name LIKE 'PowerAc%' OR name = 'PowerAndAC' ORDER BY name;
