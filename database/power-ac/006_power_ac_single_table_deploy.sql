-- 006: Power & A/C single-table structure (run after pulling the feature/power-ac-kpi branch).
-- Safe to re-run. Does not drop anything. Run 007 afterwards to remove the old Power & A/C tables.
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
-- 1. The single Power & A/C table (same layout as the other KPI data tables, plus Achievement %, allocation and audit columns)
IF OBJECT_ID(N'dbo.PowerAcPmData', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcPmData (
        id                       int           IDENTITY(1,1) NOT NULL,
        designation              nchar(20)     NOT NULL,
        [year]                   varchar(10)   NULL,
        [month]                  varchar(10)   NULL,
        scheduled                int           NULL,
        attended                 int           NULL,
        Cumulative_Sched         int           NOT NULL CONSTRAINT DF_PowerAcPmData_CumSched DEFAULT 0,
        Cumulative_Achieved      int           NOT NULL CONSTRAINT DF_PowerAcPmData_CumAch DEFAULT 0,
        is_verified              bit           NOT NULL CONSTRAINT DF_PowerAcPmData_Verified DEFAULT 0,
        pm_achievement           decimal(9,4)  NULL,
        safety_achievement       decimal(9,4)  NULL,
        corrective_achievement   decimal(9,4)  NULL,
        availability_achievement decimal(9,4)  NULL,
        area_allocation          decimal(18,4) NULL,
        entered_by               nvarchar(100) NULL,
        verified_by              nvarchar(100) NULL,
        updated_at               datetime2     NULL,
        achievements_json        nvarchar(max) NULL,
        CONSTRAINT PK_PowerAcPmData PRIMARY KEY (id)
    );
END
GO
-- Columns added after the first version of the table (no-op when present)
IF COL_LENGTH('dbo.PowerAcPmData','achievements_json') IS NULL ALTER TABLE dbo.PowerAcPmData ADD achievements_json nvarchar(max) NULL;
GO
IF COL_LENGTH('dbo.PowerAcPmData','pm_achievement') IS NULL
    ALTER TABLE dbo.PowerAcPmData ADD pm_achievement decimal(9,4) NULL, safety_achievement decimal(9,4) NULL,
        corrective_achievement decimal(9,4) NULL, availability_achievement decimal(9,4) NULL,
        area_allocation decimal(18,4) NULL, entered_by nvarchar(100) NULL, verified_by nvarchar(100) NULL, updated_at datetime2 NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PowerAcPmData_Pct')
    ALTER TABLE dbo.PowerAcPmData ADD CONSTRAINT CK_PowerAcPmData_Pct CHECK (
        (pm_achievement IS NULL OR pm_achievement BETWEEN 0 AND 100) AND
        (safety_achievement IS NULL OR safety_achievement BETWEEN 0 AND 100) AND
        (corrective_achievement IS NULL OR corrective_achievement BETWEEN 0 AND 100) AND
        (availability_achievement IS NULL OR availability_achievement BETWEEN 0 AND 100));
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_PowerAcPmData_Designation_Period')
    CREATE UNIQUE INDEX UQ_PowerAcPmData_Designation_Period ON dbo.PowerAcPmData (designation, [year], [month]);
GO
-- 2. Copy the legacy Preventive Maintenance rows (dbo.PowerAndAC, integer year and month) where they are not already present
IF OBJECT_ID(N'dbo.PowerAndAC', N'U') IS NOT NULL
    INSERT INTO dbo.PowerAcPmData (designation, [year], [month], scheduled, attended, Cumulative_Sched, Cumulative_Achieved, is_verified)
    SELECT p.Designation, CAST(p.[Year] AS varchar(10)), CHOOSE(p.[Month], 'January','February','March','April','May','June','July','August','September','October','November','December'),
           p.Scheduled, p.Attended, ISNULL(p.Cumulative_Sched, 0), ISNULL(p.Cumulative_Achieved, 0), ISNULL(p.is_verified, 0)
    FROM dbo.PowerAndAC p
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PowerAcPmData t
                      WHERE t.designation = p.Designation AND t.[year] = CAST(p.[Year] AS varchar(10))
                        AND t.[month] = CHOOSE(p.[Month], 'January','February','March','April','May','June','July','August','September','October','November','December'));
GO
-- 3. Scored-set flag on the shared KPI table (Power & A/C KPIs; new KPIs are included by default)
IF COL_LENGTH('dbo.KpiDefinition','include_in_scoring') IS NULL
    ALTER TABLE dbo.KpiDefinition ADD include_in_scoring bit NOT NULL CONSTRAINT DF_KpiDefinition_IncludeInScoring DEFAULT 1;
GO
-- 4. The four Power & A/C KPIs (only if none exist yet)
IF NOT EXISTS (SELECT 1 FROM dbo.KpiDefinition WHERE category = N'PowerAndAC')
BEGIN
    INSERT INTO dbo.KpiDefinition (perspectives, strategicObjectives, keyPerformanceIndicators, unit, descriptionOfKPI, weightage, createdAt, updatedAt, pointsApplicable, totalPoints, category)
    VALUES
    (N'Customer', N'PM', N'Preventive Maintenance', N'%', N'100', 25, CONVERT(nvarchar(19), SYSUTCDATETIME(), 120), CONVERT(nvarchar(19), SYSUTCDATETIME(), 120), 0, 0, N'PowerAndAC'),
    (N'Customer', N'SAFETY', N'Critical Safety', N'Incidents', N'0', 25, CONVERT(nvarchar(19), SYSUTCDATETIME(), 120), CONVERT(nvarchar(19), SYSUTCDATETIME(), 120), 0, 0, N'PowerAndAC'),
    (N'Customer', N'CORRECTIVE', N'Corrective Action and Maintenance Record', N'%', N'100', 25, CONVERT(nvarchar(19), SYSUTCDATETIME(), 120), CONVERT(nvarchar(19), SYSUTCDATETIME(), 120), 0, 0, N'PowerAndAC'),
    (N'Customer', N'AVAILABILITY', N'Power System Availability', N'%', N'99', 25, CONVERT(nvarchar(19), SYSUTCDATETIME(), 120), CONVERT(nvarchar(19), SYSUTCDATETIME(), 120), 0, 0, N'PowerAndAC');
END
GO
SELECT COUNT(*) AS power_ac_rows FROM dbo.PowerAcPmData;
SELECT COUNT(*) AS power_ac_kpis FROM dbo.KpiDefinition WHERE category = N'PowerAndAC';
