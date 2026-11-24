/*
================================================================================
 Power & AC KPI - deployment script 003 (run after 001)
 Adds:
   dbo.PowerAcComponentVersion   effective-dated scoring method / target / approval per component
   dbo.PowerAcAuditLog           append-only audit trail for configuration and data changes
   PowerAcKpiResult.CalculationSettingId      which settings row a snapshot used
   PowerAcKpiComponentScore.ComponentVersionId which scoring version a snapshot used
 Seeds one version per component, effective January 2026, copied from the current component row.
 It does NOT approve anything: approval state is copied exactly as it is.

 Safe to rerun. Does not change dbo.PowerAndAC, dbo.KpiDefinition, dbo.regiondata or any existing row
 other than adding the two nullable columns.
 Run with: sqlcmd -S <server> -d <database> -b -i 003_power_ac_versions_audit_deploy.sql
 Rollback: 003_power_ac_versions_audit_rollback.sql
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.PowerAcComponent', N'U') IS NULL
    THROW 53000, 'dbo.PowerAcComponent does not exist. Run 001_power_ac_kpi_deploy.sql first.', 1;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.PowerAcComponentVersion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcComponentVersion
    (
        Id                INT IDENTITY(1,1) NOT NULL,
        ComponentId       INT           NOT NULL,
        EffectiveYear     SMALLINT      NOT NULL,
        EffectiveMonth    TINYINT       NOT NULL,
        TargetValue       DECIMAL(9,4)  NOT NULL,
        ScoringMethod     NVARCHAR(40)  NOT NULL,
        ScoringParameter  DECIMAL(9,4)  NULL,
        ScoringApproved   BIT           NOT NULL CONSTRAINT DF_PowerAcComponentVersion_Approved DEFAULT (0),
        ApprovedBy        NVARCHAR(100) NULL,
        ApprovedAt        DATETIME2     NULL,
        ApprovalNote      NVARCHAR(500) NULL,
        CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_PowerAcComponentVersion_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy         NVARCHAR(100) NULL,
        UpdatedAt         DATETIME2     NULL,
        UpdatedBy         NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcComponentVersion PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcComponentVersion_Component FOREIGN KEY (ComponentId) REFERENCES dbo.PowerAcComponent (Id),
        CONSTRAINT UQ_PowerAcComponentVersion_Component_Effective UNIQUE (ComponentId, EffectiveYear, EffectiveMonth),
        CONSTRAINT CK_PowerAcComponentVersion_Month CHECK (EffectiveMonth BETWEEN 1 AND 12),
        CONSTRAINT CK_PowerAcComponentVersion_Year CHECK (EffectiveYear BETWEEN 2000 AND 9999),
        CONSTRAINT CK_PowerAcComponentVersion_Target CHECK (TargetValue >= 0),
        CONSTRAINT CK_PowerAcComponentVersion_Parameter CHECK (ScoringParameter IS NULL OR ScoringParameter >= 0),
        CONSTRAINT CK_PowerAcComponentVersion_Method CHECK (ScoringMethod IN
            (N'Pending', N'RatioCapped', N'RatioStrict', N'TargetScaled', N'DirectPercent', N'ZeroIncidentsBinary', N'DeductionPerIncident')),
        CONSTRAINT CK_PowerAcComponentVersion_PendingNotApproved CHECK (NOT (ScoringMethod = N'Pending' AND ScoringApproved = 1)),
        CONSTRAINT CK_PowerAcComponentVersion_ApprovalRecorded CHECK (ScoringApproved = 0 OR (ApprovedBy IS NOT NULL AND ApprovedAt IS NOT NULL))
    );
END;

IF OBJECT_ID(N'dbo.PowerAcAuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcAuditLog
    (
        Id           BIGINT IDENTITY(1,1) NOT NULL,
        EntityType   NVARCHAR(40)  NOT NULL,
        EntityId     INT           NOT NULL,
        Action       NVARCHAR(40)  NOT NULL,
        FromStatus   NVARCHAR(20)  NULL,
        ToStatus     NVARCHAR(20)  NULL,
        DetailsJson  NVARCHAR(MAX) NULL,
        Comment      NVARCHAR(500) NULL,
        ChangedBy    NVARCHAR(100) NULL,
        ChangedAt    DATETIME2     NOT NULL CONSTRAINT DF_PowerAcAuditLog_ChangedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_PowerAcAuditLog PRIMARY KEY (Id)
    );
    CREATE INDEX IX_PowerAcAuditLog_Entity ON dbo.PowerAcAuditLog (EntityType, EntityId);
END;

COMMIT TRANSACTION;
GO

-- New columns are added in their own batch so later batches compile against them.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.PowerAcKpiResult', N'CalculationSettingId') IS NULL
    ALTER TABLE dbo.PowerAcKpiResult ADD CalculationSettingId INT NULL;

IF COL_LENGTH(N'dbo.PowerAcKpiComponentScore', N'ComponentVersionId') IS NULL
    ALTER TABLE dbo.PowerAcKpiComponentScore ADD ComponentVersionId INT NULL;

COMMIT TRANSACTION;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FK_PowerAcKpiResult_Setting', N'F') IS NULL
    ALTER TABLE dbo.PowerAcKpiResult
        ADD CONSTRAINT FK_PowerAcKpiResult_Setting FOREIGN KEY (CalculationSettingId) REFERENCES dbo.PowerAcCalculationSetting (Id);

IF OBJECT_ID(N'dbo.FK_PowerAcKpiComponentScore_Version', N'F') IS NULL
    ALTER TABLE dbo.PowerAcKpiComponentScore
        ADD CONSTRAINT FK_PowerAcKpiComponentScore_Version FOREIGN KEY (ComponentVersionId) REFERENCES dbo.PowerAcComponentVersion (Id);

-- Seed: one version per component effective January 2026, copied exactly (approval state unchanged).
INSERT INTO dbo.PowerAcComponentVersion
    (ComponentId, EffectiveYear, EffectiveMonth, TargetValue, ScoringMethod, ScoringParameter,
     ScoringApproved, ApprovedBy, ApprovedAt, CreatedBy)
SELECT c.Id, 2026, 1, c.TargetValue, c.ScoringMethod, c.ScoringParameter,
       CASE WHEN c.ScoringApproved = 1 AND c.ApprovedBy IS NOT NULL AND c.ApprovedAt IS NOT NULL THEN 1 ELSE 0 END,
       CASE WHEN c.ScoringApproved = 1 AND c.ApprovedBy IS NOT NULL AND c.ApprovedAt IS NOT NULL THEN c.ApprovedBy END,
       CASE WHEN c.ScoringApproved = 1 AND c.ApprovedBy IS NOT NULL AND c.ApprovedAt IS NOT NULL THEN c.ApprovedAt END,
       N'deploy-003 (copied from PowerAcComponent)'
FROM dbo.PowerAcComponent c
WHERE NOT EXISTS (SELECT 1 FROM dbo.PowerAcComponentVersion v WHERE v.ComponentId = c.Id);

INSERT INTO dbo.PowerAcAuditLog (EntityType, EntityId, Action, ToStatus, DetailsJson, ChangedBy)
SELECT N'ComponentVersion', v.Id, N'Created',
       CASE WHEN v.ScoringApproved = 1 THEN N'Approved' ELSE N'Unapproved' END,
       N'{"source":"deploy-003","effective":"2026-01","method":"' + v.ScoringMethod + N'"}',
       N'deploy-003'
FROM dbo.PowerAcComponentVersion v
WHERE v.CreatedBy = N'deploy-003 (copied from PowerAcComponent)'
  AND NOT EXISTS (SELECT 1 FROM dbo.PowerAcAuditLog a WHERE a.EntityType = N'ComponentVersion' AND a.EntityId = v.Id);

COMMIT TRANSACTION;
PRINT 'Power & AC KPI deployment 003 completed.';
GO
