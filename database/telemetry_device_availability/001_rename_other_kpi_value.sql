SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE dbo.OtherKpi
SET network_engineer_kpi = N'Telemetry Device Availability'
WHERE network_engineer_kpi = N'Telemetry Network Availability';

COMMIT TRANSACTION;
