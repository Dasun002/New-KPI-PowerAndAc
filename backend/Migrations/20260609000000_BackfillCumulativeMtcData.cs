using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    public partial class BackfillCumulativeMtcData : Migration
    {
        // Recalculates CumulativeSched and CumulativeAchieved for a table.
        // cycleSize = 6  → MSAN  (Jan–Jun = cycle 1, Jul–Dec = cycle 2)
        // cycleSize = 2  → SLBN, IPNW  (Jan–Feb=1, Mar–Apr=2, …)
        private static string BuildBackfillSql(string tableName, int cycleSize) => $@"
IF OBJECT_ID(N'dbo.{tableName}', N'U') IS NOT NULL
BEGIN
    -- Map full month names to numbers
    WITH MonthMap AS (
        SELECT v.MonthName, v.MonthNum FROM (VALUES
            ('January',1),('February',2),('March',3),('April',4),
            ('May',5),('June',6),('July',7),('August',8),
            ('September',9),('October',10),('November',11),('December',12)
        ) v(MonthName, MonthNum)
    ),
    Numbered AS (
        SELECT
            t.id,
            t.designation,
            t.year,
            mm.MonthNum,
            -- cycle boundary: every {cycleSize} months resets
            ((mm.MonthNum - 1) / {cycleSize}) + 1 AS CycleNum,
            t.scheduled,
            t.attended
        FROM dbo.{tableName} t
        INNER JOIN MonthMap mm
            ON LTRIM(RTRIM(mm.MonthName)) = LTRIM(RTRIM(t.month))
        WHERE t.designation IS NOT NULL
          AND t.year IS NOT NULL
    ),
    Cumulative AS (
        SELECT
            id,
            SUM(ISNULL(scheduled, 0)) OVER (
                PARTITION BY designation, year, CycleNum
                ORDER BY MonthNum
                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
            ) AS CumSched,
            SUM(ISNULL(attended, 0)) OVER (
                PARTITION BY designation, year, CycleNum
                ORDER BY MonthNum
                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
            ) AS CumAchieved
        FROM Numbered
    )
    UPDATE t
    SET
        t.Cumulative_Sched    = c.CumSched,
        t.Cumulative_Achieved = c.CumAchieved
    FROM dbo.{tableName} t
    INNER JOIN Cumulative c ON t.id = c.id;
END;
";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MSAN — 6-month cycle (Jan–Jun, Jul–Dec)
            migrationBuilder.Sql(BuildBackfillSql("msanmtcdata", 6));

            // IPNW (VPN) — 2-month cycle
            migrationBuilder.Sql(BuildBackfillSql("ipnwmtcdata", 2));

            // SLBN — 2-month cycle
            migrationBuilder.Sql(BuildBackfillSql("slbnmtcdata", 2));

            // TOWER — 3-month cycle (quarterly)
            migrationBuilder.Sql(BuildBackfillSql("towermtcdata", 3));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Zero out all cumulative columns — reversing a recalculation is not meaningful
            foreach (var table in new[] { "msanmtcdata", "ipnwmtcdata", "slbnmtcdata", "towermtcdata" })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID(N'dbo.{table}', N'U') IS NOT NULL
    UPDATE dbo.{table} SET Cumulative_Sched = 0, Cumulative_Achieved = 0;
");
            }
        }
    }
}
