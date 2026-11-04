/*
================================================================================
 Power & AC KPI - rollback for deployment script 003
 Removes ONLY what 003 added: the two snapshot link columns, PowerAcComponentVersion and PowerAcAuditLog.
 WARNING: deletes the scoring version history and the audit trail. Back them up first, e.g.:
   SELECT * INTO dbo.PowerAcAuditLog_backup_YYYYMMDD FROM dbo.PowerAcAuditLog;
 Deploy the previous backend build before running this (the current build reads these tables).
 Run with: sqlcmd -S <server> -d <database> -b -i 003_power_ac_versions_audit_rollback.sql
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FK_PowerAcKpiComponentScore_Version', N'F') IS NOT NULL
    ALTER TABLE dbo.PowerAcKpiComponentScore DROP CONSTRAINT FK_PowerAcKpiComponentScore_Version;
IF COL_LENGTH(N'dbo.PowerAcKpiComponentScore', N'ComponentVersionId') IS NOT NULL
    ALTER TABLE dbo.PowerAcKpiComponentScore DROP COLUMN ComponentVersionId;

IF OBJECT_ID(N'dbo.FK_PowerAcKpiResult_Setting', N'F') IS NOT NULL
    ALTER TABLE dbo.PowerAcKpiResult DROP CONSTRAINT FK_PowerAcKpiResult_Setting;
IF COL_LENGTH(N'dbo.PowerAcKpiResult', N'CalculationSettingId') IS NOT NULL
    ALTER TABLE dbo.PowerAcKpiResult DROP COLUMN CalculationSettingId;

IF OBJECT_ID(N'dbo.PowerAcAuditLog', N'U') IS NOT NULL DROP TABLE dbo.PowerAcAuditLog;
IF OBJECT_ID(N'dbo.PowerAcComponentVersion', N'U') IS NOT NULL DROP TABLE dbo.PowerAcComponentVersion;

COMMIT TRANSACTION;
PRINT 'Power & AC KPI rollback 003 completed.';
GO
