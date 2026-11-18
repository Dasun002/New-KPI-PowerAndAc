/*
================================================================================
 REFERENCE ONLY - DO NOT RUN AGAINST A DATABASE THAT ALREADY HAS dbo.PowerAndAC.
 Captures the existing Preventive Maintenance table and trigger, which were created outside the
 repository (they are not in backend/Migrations or migration_script.sql). Extracted read-only from
 NWKPI_SAFETY on 2026-11-02 so the schema is under source control. The new Power & AC KPI reads this
 table and never changes it.
================================================================================
*/

/*
CREATE TABLE dbo.PowerAndAC
(
    Id                   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PowerAndAC PRIMARY KEY,
    Designation          NVARCHAR(100) NOT NULL,
    [Year]               INT NOT NULL,
    [Month]              INT NOT NULL,
    Scheduled            INT NOT NULL,
    Attended             INT NOT NULL,
    Cumulative_Sched     INT NULL,
    Cumulative_Achieved  INT NULL,
    is_verified          BIT NOT NULL CONSTRAINT DF_PowerAndAC_is_verified DEFAULT (0),
    CONSTRAINT UQ_PowerAndAC_Designation_Year_Month UNIQUE (Designation, [Year], [Month])
);
GO

-- Keeps quarter-to-date cumulative values. Quarters are calendar quarters, so the cumulative
-- values reset in January, April, July and October. Fires on INSERT and UPDATE only (not DELETE).
CREATE TRIGGER dbo.trg_PowerAndAC_Cumulative
ON dbo.PowerAndAC
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH AffectedGroups AS
    (
        SELECT DISTINCT Designation, [Year], QuarterNo = (([Month]-1)/3)
        FROM inserted
    ),
    Recalc AS
    (
        SELECT
            P.Id,
            SUM(P.Scheduled) OVER (PARTITION BY P.Designation, P.[Year], ((P.[Month]-1)/3)
                                   ORDER BY P.[Month] ROWS UNBOUNDED PRECEDING) AS Cum_Sched,
            SUM(P.Attended)  OVER (PARTITION BY P.Designation, P.[Year], ((P.[Month]-1)/3)
                                   ORDER BY P.[Month] ROWS UNBOUNDED PRECEDING) AS Cum_Achieved
        FROM dbo.PowerAndAC P
        INNER JOIN AffectedGroups AG
            ON AG.Designation = P.Designation
           AND AG.[Year] = P.[Year]
           AND AG.QuarterNo = ((P.[Month]-1)/3)
    )
    UPDATE P
    SET P.Cumulative_Sched = R.Cum_Sched,
        P.Cumulative_Achieved = R.Cum_Achieved
    FROM dbo.PowerAndAC P
    INNER JOIN Recalc R ON P.Id = R.Id;
END;
GO
*/
