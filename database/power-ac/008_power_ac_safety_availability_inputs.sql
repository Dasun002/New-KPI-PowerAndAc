-- 008: Typed inputs for Critical Safety (incident count) and Power System Availability (actual %).
-- Adds two columns to dbo.PowerAcPmData. It does not change or delete any existing value:
--   * existing safety_achievement and availability_achievement values stay exactly as they are;
--   * the new input columns are NULL for every existing row until someone enters a value.
-- Safe to re-run. Run after 006 and 007. Take a backup first.
--
-- Rollback (only if the previous application version is redeployed; this drops the typed inputs):
--   ALTER TABLE dbo.PowerAcPmData DROP CONSTRAINT IF EXISTS CK_PowerAcPmData_SafetyIncidents;
--   ALTER TABLE dbo.PowerAcPmData DROP CONSTRAINT IF EXISTS CK_PowerAcPmData_AvailabilityActual;
--   ALTER TABLE dbo.PowerAcPmData DROP COLUMN IF EXISTS safety_incident_count, availability_actual_pct;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.PowerAcPmData', N'U') IS NULL
BEGIN
    THROW 50080, 'dbo.PowerAcPmData does not exist. Run 006 first.', 1;
END
GO

IF COL_LENGTH(N'dbo.PowerAcPmData', N'safety_incident_count') IS NULL
    ALTER TABLE dbo.PowerAcPmData ADD safety_incident_count int NULL;
GO

IF COL_LENGTH(N'dbo.PowerAcPmData', N'availability_actual_pct') IS NULL
    ALTER TABLE dbo.PowerAcPmData ADD availability_actual_pct decimal(9,4) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PowerAcPmData_SafetyIncidents')
    ALTER TABLE dbo.PowerAcPmData ADD CONSTRAINT CK_PowerAcPmData_SafetyIncidents
        CHECK (safety_incident_count IS NULL OR safety_incident_count >= 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PowerAcPmData_AvailabilityActual')
    ALTER TABLE dbo.PowerAcPmData ADD CONSTRAINT CK_PowerAcPmData_AvailabilityActual
        CHECK (availability_actual_pct IS NULL OR (availability_actual_pct >= 0 AND availability_actual_pct <= 100));
GO

-- Read-only check: the new columns exist and no existing achievement value was changed by this script.
SELECT
    COUNT(*)                                                      AS power_ac_rows,
    SUM(CASE WHEN safety_achievement IS NOT NULL THEN 1 ELSE 0 END) AS rows_with_legacy_safety_pct,
    SUM(CASE WHEN safety_incident_count IS NOT NULL THEN 1 ELSE 0 END) AS rows_with_incident_count
FROM dbo.PowerAcPmData;
GO
