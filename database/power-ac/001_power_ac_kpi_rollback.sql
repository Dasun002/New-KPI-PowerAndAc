/*
================================================================================
 Power & AC KPI - rollback for deployment script 001
 Removes ONLY the tables created by 001_power_ac_kpi_deploy.sql, in foreign-key order.
 Does NOT touch dbo.PowerAndAC, dbo.KpiDefinition, dbo.regiondata, dbo.OverallKpiResult or any other table.

 WARNING: this permanently deletes all allocations, component data, configuration history and
 calculation snapshots entered through the new Power & AC screens. Back them up first if needed, e.g.:
   SELECT * INTO dbo.PowerAcAreaAllocation_backup_YYYYMMDD FROM dbo.PowerAcAreaAllocation;
 Review before running. Never run automatically.
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Objects added by 003 (if present) reference the tables below, so remove them first.
IF OBJECT_ID(N'dbo.PowerAcAuditLog', N'U')            IS NOT NULL DROP TABLE dbo.PowerAcAuditLog;
IF OBJECT_ID(N'dbo.FK_PowerAcKpiComponentScore_Version', N'F') IS NOT NULL
    ALTER TABLE dbo.PowerAcKpiComponentScore DROP CONSTRAINT FK_PowerAcKpiComponentScore_Version;
IF OBJECT_ID(N'dbo.FK_PowerAcKpiResult_Setting', N'F') IS NOT NULL
    ALTER TABLE dbo.PowerAcKpiResult DROP CONSTRAINT FK_PowerAcKpiResult_Setting;
IF OBJECT_ID(N'dbo.PowerAcComponentVersion', N'U')    IS NOT NULL DROP TABLE dbo.PowerAcComponentVersion;

IF OBJECT_ID(N'dbo.PowerAcKpiComponentScore', N'U')   IS NOT NULL DROP TABLE dbo.PowerAcKpiComponentScore;
IF OBJECT_ID(N'dbo.PowerAcKpiResult', N'U')           IS NOT NULL DROP TABLE dbo.PowerAcKpiResult;
IF OBJECT_ID(N'dbo.PowerAcComponentResult', N'U')     IS NOT NULL DROP TABLE dbo.PowerAcComponentResult;
IF OBJECT_ID(N'dbo.PowerAcAreaAllocation', N'U')      IS NOT NULL DROP TABLE dbo.PowerAcAreaAllocation;
IF OBJECT_ID(N'dbo.PowerAcGroupUserAssignment', N'U') IS NOT NULL DROP TABLE dbo.PowerAcGroupUserAssignment;
IF OBJECT_ID(N'dbo.PowerAcCalculationSetting', N'U')  IS NOT NULL DROP TABLE dbo.PowerAcCalculationSetting;
IF OBJECT_ID(N'dbo.PowerAcComponentWeight', N'U')     IS NOT NULL DROP TABLE dbo.PowerAcComponentWeight;
IF OBJECT_ID(N'dbo.PowerAcEngineerGroup', N'U')       IS NOT NULL DROP TABLE dbo.PowerAcEngineerGroup;
IF OBJECT_ID(N'dbo.PowerAcComponent', N'U')           IS NOT NULL DROP TABLE dbo.PowerAcComponent;

COMMIT TRANSACTION;
PRINT 'Power & AC KPI rollback 001 completed.';
GO

/*
 Application rollback: deploy the previous backend build, or keep this build with
 "PowerAc:UseComponentKpiInOverall": false (the default). The legacy Power & AC branch in
 OverallKpiResultsController is unchanged and keeps working without the new tables only if the new
 controllers are not called; the overall calculation itself does not read the new tables while the
 switch is false.
*/
