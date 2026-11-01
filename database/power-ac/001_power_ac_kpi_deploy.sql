/*
================================================================================
 Power & AC KPI - deployment script 001
 Target      : SQL Server, NWKPI database family (schema dbo)
 Purpose     : Adds the four-component Power & AC KPI tables and seed configuration.
 Safe to rerun: yes. Every object is created only if missing; seed rows are inserted only if missing.
 Does NOT    : modify, delete or overwrite dbo.PowerAndAC, dbo.KpiDefinition, dbo.regiondata or any existing row.
 Does NOT    : use or touch __EFMigrationsHistory. The repository migrations do not match the database history,
               so this script is the deployment mechanism for these tables. Do not run "dotnet ef database update".

 Review before running. Take a full backup first. Run on a non-production copy before production.
 Rollback    : 001_power_ac_kpi_rollback.sql
 Verification: 002_power_ac_kpi_verify.sql (read-only)
================================================================================
*/
-- Run with: sqlcmd ... -b -i 001_power_ac_kpi_deploy.sql   (-b stops on the first error)
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- required for the filtered unique indexes below
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

-- Pre-checks (same batch as the changes, so a failure stops everything).
IF OBJECT_ID(N'dbo.regiondata', N'U') IS NULL
    THROW 51000, 'dbo.regiondata does not exist. Aborting.', 1;
IF OBJECT_ID(N'dbo.PowerAndAC', N'U') IS NULL
    THROW 51001, 'dbo.PowerAndAC does not exist. Aborting.', 1;
IF OBJECT_ID(N'dbo.KpiDefinition', N'U') IS NULL
    THROW 51002, 'dbo.KpiDefinition does not exist. Aborting.', 1;
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 51003, 'dbo.Users does not exist. Aborting.', 1;

BEGIN TRANSACTION;

------------------------------------------------------------------------------
-- 1. PowerAcComponent
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PowerAcComponent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcComponent
    (
        Id                INT IDENTITY(1,1) NOT NULL,
        Code              NVARCHAR(30)  NOT NULL,
        Name              NVARCHAR(150) NOT NULL,
        TargetValue       DECIMAL(9,4)  NOT NULL,
        TargetUnit        NVARCHAR(20)  NOT NULL CONSTRAINT DF_PowerAcComponent_TargetUnit DEFAULT (N'%'),
        DataSource        NVARCHAR(30)  NOT NULL,
        ScoringMethod     NVARCHAR(40)  NOT NULL CONSTRAINT DF_PowerAcComponent_ScoringMethod DEFAULT (N'Pending'),
        ScoringParameter  DECIMAL(9,4)  NULL,
        ScoringApproved   BIT           NOT NULL CONSTRAINT DF_PowerAcComponent_ScoringApproved DEFAULT (0),
        ApprovedBy        NVARCHAR(100) NULL,
        ApprovedAt        DATETIME2     NULL,
        SortOrder         INT           NOT NULL CONSTRAINT DF_PowerAcComponent_SortOrder DEFAULT (0),
        IsActive          BIT           NOT NULL CONSTRAINT DF_PowerAcComponent_IsActive DEFAULT (1),
        CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_PowerAcComponent_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy         NVARCHAR(100) NULL,
        UpdatedAt         DATETIME2     NULL,
        UpdatedBy         NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcComponent PRIMARY KEY (Id),
        CONSTRAINT UQ_PowerAcComponent_Code UNIQUE (Code),
        CONSTRAINT CK_PowerAcComponent_DataSource CHECK (DataSource IN (N'PowerAndAC', N'ComponentResult')),
        CONSTRAINT CK_PowerAcComponent_ScoringMethod CHECK (ScoringMethod IN
            (N'Pending', N'RatioCapped', N'RatioStrict', N'TargetScaled', N'DirectPercent', N'ZeroIncidentsBinary', N'DeductionPerIncident')),
        CONSTRAINT CK_PowerAcComponent_Target CHECK (TargetValue >= 0),
        CONSTRAINT CK_PowerAcComponent_Parameter CHECK (ScoringParameter IS NULL OR ScoringParameter >= 0),
        CONSTRAINT CK_PowerAcComponent_PendingNotApproved CHECK (NOT (ScoringMethod = N'Pending' AND ScoringApproved = 1))
    );
END;

------------------------------------------------------------------------------
-- 2. PowerAcComponentWeight (effective-dated; history is kept)
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PowerAcComponentWeight', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcComponentWeight
    (
        Id              INT IDENTITY(1,1) NOT NULL,
        ComponentId     INT           NOT NULL,
        EffectiveYear   SMALLINT      NOT NULL,
        EffectiveMonth  TINYINT       NOT NULL,
        WeightPercent   DECIMAL(7,4)  NOT NULL,
        CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_PowerAcComponentWeight_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy       NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcComponentWeight PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcComponentWeight_Component FOREIGN KEY (ComponentId) REFERENCES dbo.PowerAcComponent (Id),
        CONSTRAINT UQ_PowerAcComponentWeight_Component_Effective UNIQUE (ComponentId, EffectiveYear, EffectiveMonth),
        CONSTRAINT CK_PowerAcComponentWeight_Weight CHECK (WeightPercent >= 0 AND WeightPercent <= 100),
        CONSTRAINT CK_PowerAcComponentWeight_Month CHECK (EffectiveMonth BETWEEN 1 AND 12),
        CONSTRAINT CK_PowerAcComponentWeight_Year CHECK (EffectiveYear BETWEEN 2000 AND 9999)
    );
END;

------------------------------------------------------------------------------
-- 3. PowerAcEngineerGroup ('Power & AC'!C19:C38 mapped to dbo.regiondata by id)
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PowerAcEngineerGroup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcEngineerGroup
    (
        Id                      INT IDENTITY(1,1) NOT NULL,
        Code                    NVARCHAR(30)  NOT NULL,
        WorkbookLabel           NVARCHAR(50)  NOT NULL,
        WorkbookAllocationCell  NVARCHAR(10)  NOT NULL,
        RegionDataId            INT           NULL,
        PmDesignation           NVARCHAR(100) NULL,
        MappingVerified         BIT           NOT NULL CONSTRAINT DF_PowerAcEngineerGroup_MappingVerified DEFAULT (0),
        MappingNote             NVARCHAR(500) NULL,
        SortOrder               INT           NOT NULL CONSTRAINT DF_PowerAcEngineerGroup_SortOrder DEFAULT (0),
        IsActive                BIT           NOT NULL CONSTRAINT DF_PowerAcEngineerGroup_IsActive DEFAULT (1),
        CreatedAt               DATETIME2     NOT NULL CONSTRAINT DF_PowerAcEngineerGroup_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy               NVARCHAR(100) NULL,
        UpdatedAt               DATETIME2     NULL,
        UpdatedBy               NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcEngineerGroup PRIMARY KEY (Id),
        CONSTRAINT UQ_PowerAcEngineerGroup_Code UNIQUE (Code),
        CONSTRAINT UQ_PowerAcEngineerGroup_Cell UNIQUE (WorkbookAllocationCell),
        CONSTRAINT FK_PowerAcEngineerGroup_RegionData FOREIGN KEY (RegionDataId) REFERENCES dbo.regiondata (id),
        CONSTRAINT CK_PowerAcEngineerGroup_VerifiedNeedsMapping CHECK
            (MappingVerified = 0 OR (RegionDataId IS NOT NULL AND PmDesignation IS NOT NULL))
    );
    CREATE UNIQUE INDEX UX_PowerAcEngineerGroup_RegionData ON dbo.PowerAcEngineerGroup (RegionDataId) WHERE RegionDataId IS NOT NULL;
    CREATE UNIQUE INDEX UX_PowerAcEngineerGroup_PmDesignation ON dbo.PowerAcEngineerGroup (PmDesignation) WHERE PmDesignation IS NOT NULL;
END;

------------------------------------------------------------------------------
-- 4. PowerAcGroupUserAssignment (which users may enter data for which group)
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PowerAcGroupUserAssignment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcGroupUserAssignment
    (
        Id               INT IDENTITY(1,1) NOT NULL,
        EngineerGroupId  INT           NOT NULL,
        UserId           INT           NOT NULL,
        AssignedAt       DATETIME2     NOT NULL CONSTRAINT DF_PowerAcGroupUserAssignment_AssignedAt DEFAULT (SYSUTCDATETIME()),
        AssignedBy       NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcGroupUserAssignment PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcGroupUserAssignment_Group FOREIGN KEY (EngineerGroupId) REFERENCES dbo.PowerAcEngineerGroup (Id) ON DELETE CASCADE,
        CONSTRAINT FK_PowerAcGroupUserAssignment_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
        CONSTRAINT UQ_PowerAcGroupUserAssignment_Group_User UNIQUE (EngineerGroupId, UserId)
    );
    CREATE INDEX IX_PowerAcGroupUserAssignment_User ON dbo.PowerAcGroupUserAssignment (UserId);
END;

------------------------------------------------------------------------------
-- 5. PowerAcAreaAllocation ('Power & AC'!D19:D38, effective-dated; history is kept)
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PowerAcAreaAllocation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcAreaAllocation
    (
        Id               INT IDENTITY(1,1) NOT NULL,
        EngineerGroupId  INT           NOT NULL,
        EffectiveYear    SMALLINT      NOT NULL,
        EffectiveMonth   TINYINT       NOT NULL,
        AllocationValue  DECIMAL(18,4) NOT NULL,
        Remarks          NVARCHAR(500) NULL,
        CreatedAt        DATETIME2     NOT NULL CONSTRAINT DF_PowerAcAreaAllocation_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy        NVARCHAR(100) NULL,
        UpdatedAt        DATETIME2     NULL,
        UpdatedBy        NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcAreaAllocation PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcAreaAllocation_Group FOREIGN KEY (EngineerGroupId) REFERENCES dbo.PowerAcEngineerGroup (Id),
        CONSTRAINT UQ_PowerAcAreaAllocation_Group_Effective UNIQUE (EngineerGroupId, EffectiveYear, EffectiveMonth),
        CONSTRAINT CK_PowerAcAreaAllocation_Value CHECK (AllocationValue >= 0),
        CONSTRAINT CK_PowerAcAreaAllocation_Month CHECK (EffectiveMonth BETWEEN 1 AND 12),
        CONSTRAINT CK_PowerAcAreaAllocation_Year CHECK (EffectiveYear BETWEEN 2000 AND 9999)
    );
END;

------------------------------------------------------------------------------
-- 6. PowerAcComponentResult (monthly Safety / Corrective / Availability data)
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PowerAcComponentResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcComponentResult
    (
        Id                  INT IDENTITY(1,1) NOT NULL,
        EngineerGroupId     INT           NOT NULL,
        Year                SMALLINT      NOT NULL,
        Month               TINYINT       NOT NULL,
        ComponentId         INT           NOT NULL,
        ApplicableCount     DECIMAL(18,4) NULL,
        CompletedCount      DECIMAL(18,4) NULL,
        IncidentCount       INT           NULL,
        AchievementPercent  DECIMAL(9,4)  NULL,
        Status              NVARCHAR(20)  NOT NULL CONSTRAINT DF_PowerAcComponentResult_Status DEFAULT (N'Draft'),
        Remarks             NVARCHAR(500) NULL,
        ReviewComment       NVARCHAR(500) NULL,
        SubmittedBy         NVARCHAR(100) NULL,
        SubmittedAt         DATETIME2     NULL,
        VerifiedBy          NVARCHAR(100) NULL,
        VerifiedAt          DATETIME2     NULL,
        CreatedAt           DATETIME2     NOT NULL CONSTRAINT DF_PowerAcComponentResult_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           NVARCHAR(100) NULL,
        UpdatedAt           DATETIME2     NULL,
        UpdatedBy           NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcComponentResult PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcComponentResult_Group FOREIGN KEY (EngineerGroupId) REFERENCES dbo.PowerAcEngineerGroup (Id),
        CONSTRAINT FK_PowerAcComponentResult_Component FOREIGN KEY (ComponentId) REFERENCES dbo.PowerAcComponent (Id),
        CONSTRAINT UQ_PowerAcComponentResult_Group_Period_Component UNIQUE (EngineerGroupId, Year, Month, ComponentId),
        CONSTRAINT CK_PowerAcComponentResult_Month CHECK (Month BETWEEN 1 AND 12),
        CONSTRAINT CK_PowerAcComponentResult_Year CHECK (Year BETWEEN 2000 AND 9999),
        CONSTRAINT CK_PowerAcComponentResult_Applicable CHECK (ApplicableCount IS NULL OR ApplicableCount >= 0),
        CONSTRAINT CK_PowerAcComponentResult_Completed CHECK (CompletedCount IS NULL OR CompletedCount >= 0),
        CONSTRAINT CK_PowerAcComponentResult_Incidents CHECK (IncidentCount IS NULL OR IncidentCount >= 0),
        CONSTRAINT CK_PowerAcComponentResult_Percent CHECK (AchievementPercent IS NULL OR (AchievementPercent >= 0 AND AchievementPercent <= 100)),
        CONSTRAINT CK_PowerAcComponentResult_Status CHECK (Status IN (N'Draft', N'Submitted', N'Verified', N'Rejected'))
    );
    CREATE INDEX IX_PowerAcComponentResult_Period_Status ON dbo.PowerAcComponentResult (Year, Month, Status);
END;

------------------------------------------------------------------------------
-- 7. PowerAcCalculationSetting (effective-dated points formula and verification policy)
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PowerAcCalculationSetting', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcCalculationSetting
    (
        Id                    INT IDENTITY(1,1) NOT NULL,
        EffectiveYear         SMALLINT      NOT NULL,
        EffectiveMonth        TINYINT       NOT NULL,
        KpiDefinitionId       INT           NOT NULL,
        PointsMethod          NVARCHAR(30)  NOT NULL,
        CompositeTarget       DECIMAL(9,4)  NOT NULL CONSTRAINT DF_PowerAcCalculationSetting_CompositeTarget DEFAULT (1),
        PointsMethodApproved  BIT           NOT NULL CONSTRAINT DF_PowerAcCalculationSetting_Approved DEFAULT (0),
        RequireVerifiedData   BIT           NOT NULL CONSTRAINT DF_PowerAcCalculationSetting_RequireVerified DEFAULT (1),
        CreatedAt             DATETIME2     NOT NULL CONSTRAINT DF_PowerAcCalculationSetting_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy             NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcCalculationSetting PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcCalculationSetting_KpiDefinition FOREIGN KEY (KpiDefinitionId) REFERENCES dbo.KpiDefinition (id),
        CONSTRAINT UQ_PowerAcCalculationSetting_Effective UNIQUE (EffectiveYear, EffectiveMonth),
        CONSTRAINT CK_PowerAcCalculationSetting_Method CHECK (PointsMethod IN (N'TargetScaled', N'Linear')),
        CONSTRAINT CK_PowerAcCalculationSetting_Target CHECK (CompositeTarget > 0),
        CONSTRAINT CK_PowerAcCalculationSetting_Month CHECK (EffectiveMonth BETWEEN 1 AND 12)
    );
END;

------------------------------------------------------------------------------
-- 8. PowerAcKpiResult and 9. PowerAcKpiComponentScore (calculation snapshots)
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PowerAcKpiResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcKpiResult
    (
        Id                    INT IDENTITY(1,1) NOT NULL,
        EngineerGroupId       INT           NOT NULL,
        Year                  SMALLINT      NOT NULL,
        PeriodType            NVARCHAR(10)  NOT NULL,
        PeriodNumber          TINYINT       NOT NULL,
        Status                NVARCHAR(20)  NOT NULL,
        CompositeAchievement  DECIMAL(9,6)  NULL,
        PointsApplicable      DECIMAL(18,4) NULL,
        AllocationValue       DECIMAL(18,4) NULL,
        TotalAllocation       DECIMAL(18,4) NULL,
        MaximumPoints         DECIMAL(18,4) NULL,
        PointsAchieved        DECIMAL(18,4) NULL,
        PointsMethod          NVARCHAR(30)  NOT NULL,
        CompositeTarget       DECIMAL(9,4)  NOT NULL,
        IssuesJson            NVARCHAR(MAX) NULL,
        CalculatedAt          DATETIME2     NOT NULL CONSTRAINT DF_PowerAcKpiResult_CalculatedAt DEFAULT (SYSUTCDATETIME()),
        CalculatedBy          NVARCHAR(100) NULL,
        CONSTRAINT PK_PowerAcKpiResult PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcKpiResult_Group FOREIGN KEY (EngineerGroupId) REFERENCES dbo.PowerAcEngineerGroup (Id),
        CONSTRAINT UQ_PowerAcKpiResult_Group_Period UNIQUE (EngineerGroupId, Year, PeriodType, PeriodNumber),
        CONSTRAINT CK_PowerAcKpiResult_PeriodType CHECK (PeriodType IN (N'Month', N'Quarter')),
        CONSTRAINT CK_PowerAcKpiResult_Period CHECK
            ((PeriodType = N'Month' AND PeriodNumber BETWEEN 1 AND 12) OR (PeriodType = N'Quarter' AND PeriodNumber BETWEEN 1 AND 4)),
        CONSTRAINT CK_PowerAcKpiResult_Status CHECK (Status IN (N'Complete', N'Incomplete', N'Pending'))
    );
    CREATE INDEX IX_PowerAcKpiResult_Period ON dbo.PowerAcKpiResult (Year, PeriodType, PeriodNumber);
END;

IF OBJECT_ID(N'dbo.PowerAcKpiComponentScore', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcKpiComponentScore
    (
        Id                INT IDENTITY(1,1) NOT NULL,
        KpiResultId       INT           NOT NULL,
        ComponentId       INT           NOT NULL,
        WeightPercent     DECIMAL(7,4)  NULL,
        ScoringMethod     NVARCHAR(40)  NOT NULL,
        ApplicableCount   DECIMAL(18,4) NULL,
        CompletedCount    DECIMAL(18,4) NULL,
        IncidentCount     INT           NULL,
        AchievementRatio  DECIMAL(9,6)  NULL,
        Score             DECIMAL(9,6)  NULL,
        WeightedScore     DECIMAL(9,6)  NULL,
        Status            NVARCHAR(20)  NOT NULL,
        Message           NVARCHAR(500) NULL,
        CONSTRAINT PK_PowerAcKpiComponentScore PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcKpiComponentScore_Result FOREIGN KEY (KpiResultId) REFERENCES dbo.PowerAcKpiResult (Id) ON DELETE CASCADE,
        CONSTRAINT FK_PowerAcKpiComponentScore_Component FOREIGN KEY (ComponentId) REFERENCES dbo.PowerAcComponent (Id),
        CONSTRAINT UQ_PowerAcKpiComponentScore_Result_Component UNIQUE (KpiResultId, ComponentId),
        CONSTRAINT CK_PowerAcKpiComponentScore_Status CHECK (Status IN (N'Complete', N'Incomplete', N'Pending'))
    );
END;

------------------------------------------------------------------------------
-- Seed: components (scoring methods start UNAPPROVED; an Admin approves them in the UI)
------------------------------------------------------------------------------
MERGE dbo.PowerAcComponent AS t
USING (VALUES
    (N'PM',           N'Preventive Maintenance',                  CAST(100 AS DECIMAL(9,4)), N'%',         N'PowerAndAC',      N'RatioCapped', 1),
    (N'SAFETY',       N'Critical Safety',                         CAST(0   AS DECIMAL(9,4)), N'Incidents', N'ComponentResult', N'Pending',     2),
    (N'CORRECTIVE',   N'Corrective Action and Maintenance Record', CAST(100 AS DECIMAL(9,4)), N'%',         N'ComponentResult', N'RatioCapped', 3),
    (N'AVAILABILITY', N'Power System Availability',               CAST(99  AS DECIMAL(9,4)), N'%',         N'ComponentResult', N'RatioCapped', 4)
) AS s (Code, Name, TargetValue, TargetUnit, DataSource, ScoringMethod, SortOrder)
ON t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Name, TargetValue, TargetUnit, DataSource, ScoringMethod, ScoringApproved, SortOrder, IsActive, CreatedBy)
    VALUES (s.Code, s.Name, s.TargetValue, s.TargetUnit, s.DataSource, s.ScoringMethod, 0, s.SortOrder, 1, N'deploy-001');

------------------------------------------------------------------------------
-- Seed: temporary default weights, 25% each, effective January 2026
------------------------------------------------------------------------------
INSERT INTO dbo.PowerAcComponentWeight (ComponentId, EffectiveYear, EffectiveMonth, WeightPercent, CreatedBy)
SELECT c.Id, 2026, 1, 25.0000, N'deploy-001 (temporary default)'
FROM dbo.PowerAcComponent c
WHERE c.Code IN (N'PM', N'SAFETY', N'CORRECTIVE', N'AVAILABILITY')
  AND NOT EXISTS (SELECT 1 FROM dbo.PowerAcComponentWeight w
                  WHERE w.ComponentId = c.Id AND w.EffectiveYear = 2026 AND w.EffectiveMonth = 1);

------------------------------------------------------------------------------
-- Seed: calculation settings effective January 2026 (points formula NOT approved; an Admin approves it in the app)
-- Points formula from workbook 'KPI Calculation'!K19: IF(I19>1, J19, J19*I19/1)  -> TargetScaled, target 1
-- KPI definition 2017 = "O&M of Power & Aircondition" (Points Applicable 5000 in the inspected databases)
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.PowerAcCalculationSetting WHERE EffectiveYear = 2026 AND EffectiveMonth = 1)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.KpiDefinition WHERE id = 2017)
        THROW 51004, 'KpiDefinition 2017 (Power & AC) was not found. Insert the settings row manually with the correct KPI id.', 1;

    INSERT INTO dbo.PowerAcCalculationSetting
        (EffectiveYear, EffectiveMonth, KpiDefinitionId, PointsMethod, CompositeTarget, PointsMethodApproved, RequireVerifiedData, CreatedBy)
    -- PointsMethodApproved = 0: the formula comes from the workbook, but approval is a business decision made in the app.
    VALUES (2026, 1, 2017, N'TargetScaled', 1.0000, 0, 1, N'deploy-001 (workbook KPI Calculation!K19, not approved)');
END;

------------------------------------------------------------------------------
-- Seed: 20 engineer groups from 'Power & AC'!C19:C38
-- A mapping is marked verified only when the workbook label and the 'KPI Calculation' row 19 formula agree,
-- the regiondata row exists, and the PowerAndAC designation exists. Ambiguous rows stay unverified.
------------------------------------------------------------------------------
DECLARE @groups TABLE
(
    SortOrder INT, Cell NVARCHAR(10), Label NVARCHAR(50), Code NVARCHAR(30),
    NetworkEngineer NVARCHAR(50), PmDesignation NVARCHAR(100), Confirmed BIT, Note NVARCHAR(500)
);
INSERT INTO @groups VALUES
 ( 1, N'D19', N'E/NW/ EPS ', N'EP',    N'EP',    N'NW/EP',    0, N'Label "E/NW/ EPS" does not exactly match regiondata EP. ''KPI Calculation''!BF19 (NW/EP column) references D19. Confirm before verifying.'),
 ( 2, N'D20', N'E/NW/CPN',   N'CPN',   N'CPN',   N'NW/CPN',   1, N'Label and ''KPI Calculation''!AK19 agree.'),
 ( 3, N'D21', N'E/NW/CPS',   N'CPS',   N'CPS',   N'NW/CPS',   1, N'Label and ''KPI Calculation''!AN19 agree.'),
 ( 4, N'D22', N'E/NW/NCP',   N'NCP',   N'NCP',   N'NW/NCP',   1, N'Label and ''KPI Calculation''!BO19 agree.'),
 ( 5, N'D23', N'E/NW/NP2',   N'NP-2',  N'NP-2',  N'NW/NP-2',  1, N'Label and ''KPI Calculation''!BL19 agree.'),
 ( 6, N'D24', N'E/NW/NP1',   N'NP-1',  N'NP-1',  N'NW/NP-1',  1, N'Label and ''KPI Calculation''!BI19 agree.'),
 ( 7, N'D25', N'E/NW/NWPE',  N'NWPE',  N'NWPE',  N'NW/NWPE',  1, N'Confirmed mapping NW/NWP-E -> D25. Label and ''KPI Calculation''!AE19 agree.'),
 ( 8, N'D26', N'E/NW/NWPW',  N'NWPW',  N'NWPW',  N'NW/NWPW',  1, N'Label and ''KPI Calculation''!AH19 agree.'),
 ( 9, N'D27', N'E/NW/SAB ',  N'SAB',   N'SAB',   N'NW/SAB',   1, N'Label and ''KPI Calculation''!AT19 agree.'),
 (10, N'D28', N'E/NW/SPE',   N'SPE',   N'SPE',   N'NW/SPE',   1, N'Label and ''KPI Calculation''!AW19 agree.'),
 (11, N'D29', N'E/NW/SPW',   N'SPW',   N'SPW',   N'NW/SPW',   1, N'Label and ''KPI Calculation''!AZ19 agree.'),
 (12, N'D30', N'E/NW/UVA',   N'UVA',   N'UVA',   N'NW/UVA',   1, N'Label and ''KPI Calculation''!AQ19 agree.'),
 (13, N'D31', N'E/NW/WPC1',  N'WPC-1', N'WPC-1', N'NW/WPC-1', 1, N'Label and ''KPI Calculation''!J19 agree.'),
 (14, N'D32', N'E/NW/WPC2',  N'WPC-2', N'WPC-2', N'NW/WPC-2', 1, N'Label and ''KPI Calculation''!M19 agree.'),
 (15, N'D33', N'E/NW/WPE',   N'WPE',   N'WPE',   N'NW/WPE',   1, N'Label and ''KPI Calculation''!P19 agree.'),
 (16, N'D34', N'E/NW/WPN',   N'WPN',   N'WPN',   N'NW/WPN',   1, N'Label and ''KPI Calculation''!Y19 agree.'),
 (17, N'D35', N'E/NW/WPNE',  N'WPNE',  N'WPNE',  N'NW/WPNE',  1, N'Label and ''KPI Calculation''!AB19 agree.'),
 (18, N'D36', N'E/NW/WPS',   N'WPS',   N'WPS',   N'NW/WPS',   1, N'Mapped by label. Workbook defect: ''KPI Calculation''!BC19 (WPS column) is #REF!.'),
 (19, N'D37', N'E/NW/WPSE',  N'WPSE',  N'WPSE',  N'NW/WPSE',  1, N'Confirmed mapping NW/WP S-E -> D37 (matches label). Workbook defect: ''KPI Calculation''!V19 references D36 instead.'),
 (20, N'D38', N'E/NW/WPSW',  N'WPSW',  N'WPSW',  N'NW/WPSW',  1, N'Confirmed mapping NW/WP S-W -> D38. Label and ''KPI Calculation''!S19 agree.');

INSERT INTO dbo.PowerAcEngineerGroup
    (Code, WorkbookLabel, WorkbookAllocationCell, RegionDataId, PmDesignation, MappingVerified, MappingNote, SortOrder, IsActive, CreatedBy)
SELECT
    g.Code,
    g.Label,
    g.Cell,
    r.id,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.PowerAndAC p WHERE p.Designation = g.PmDesignation) THEN g.PmDesignation END,
    CASE WHEN g.Confirmed = 1
              AND r.id IS NOT NULL
              AND EXISTS (SELECT 1 FROM dbo.PowerAndAC p WHERE p.Designation = g.PmDesignation)
         THEN 1 ELSE 0 END,
    g.Note
        + CASE WHEN r.id IS NULL THEN N' regiondata row "' + g.NetworkEngineer + N'" not found.' ELSE N'' END
        + CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.PowerAndAC p WHERE p.Designation = g.PmDesignation)
               THEN N' PowerAndAC designation "' + g.PmDesignation + N'" not found.' ELSE N'' END,
    g.SortOrder,
    1,
    N'deploy-001'
FROM @groups g
OUTER APPLY (SELECT TOP (1) rd.id FROM dbo.regiondata rd WHERE rd.network_engineer = g.NetworkEngineer ORDER BY rd.id) r
WHERE NOT EXISTS (SELECT 1 FROM dbo.PowerAcEngineerGroup e WHERE e.WorkbookAllocationCell = g.Cell);

-- Area allocations are intentionally NOT seeded. Engineers enter them in the application.

COMMIT TRANSACTION;
PRINT 'Power & AC KPI deployment 001 completed.';
GO
