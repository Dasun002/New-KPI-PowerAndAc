-- Backfill CumulativeSched / CumulativeAchieved for all four MTC tables
-- MSAN: 6-month cycle (Jan-Jun = cycle 1, Jul-Dec = cycle 2)
-- IPNW / SLBN: 2-month cycle (Jan-Feb=1, Mar-Apr=2, ...)
-- TOWER: 3-month cycle (Jan-Mar=1, Apr-Jun=2, Jul-Sep=3, Oct-Dec=4)

-- ============================================================
-- MSAN (cycleSize = 6)
-- ============================================================
IF OBJECT_ID(N'dbo.msanmtcdata', N'U') IS NOT NULL
BEGIN
    WITH MonthMap AS (
        SELECT v.MonthName, v.MonthNum FROM (VALUES
            ('January',1),('February',2),('March',3),('April',4),
            ('May',5),('June',6),('July',7),('August',8),
            ('September',9),('October',10),('November',11),('December',12)
        ) v(MonthName, MonthNum)
    ),
    Numbered AS (
        SELECT t.id, t.designation, t.year, mm.MonthNum,
            ((mm.MonthNum - 1) / 6) + 1 AS CycleNum,
            t.scheduled, t.attended
        FROM dbo.msanmtcdata t
        INNER JOIN MonthMap mm ON LTRIM(RTRIM(mm.MonthName)) = LTRIM(RTRIM(t.month))
        WHERE t.designation IS NOT NULL AND t.year IS NOT NULL
    ),
    Cumulative AS (
        SELECT id,
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
    SET t.Cumulative_Sched = c.CumSched,
        t.Cumulative_Achieved = c.CumAchieved
    FROM dbo.msanmtcdata t
    INNER JOIN Cumulative c ON t.id = c.id;

    PRINT 'MSAN: ' + CAST(@@ROWCOUNT AS varchar) + ' rows updated';
END;

-- ============================================================
-- IPNW (cycleSize = 2)
-- ============================================================
IF OBJECT_ID(N'dbo.ipnwmtcdata', N'U') IS NOT NULL
BEGIN
    WITH MonthMap AS (
        SELECT v.MonthName, v.MonthNum FROM (VALUES
            ('January',1),('February',2),('March',3),('April',4),
            ('May',5),('June',6),('July',7),('August',8),
            ('September',9),('October',10),('November',11),('December',12)
        ) v(MonthName, MonthNum)
    ),
    Numbered AS (
        SELECT t.id, t.designation, t.year, mm.MonthNum,
            ((mm.MonthNum - 1) / 2) + 1 AS CycleNum,
            t.scheduled, t.attended
        FROM dbo.ipnwmtcdata t
        INNER JOIN MonthMap mm ON LTRIM(RTRIM(mm.MonthName)) = LTRIM(RTRIM(t.month))
        WHERE t.designation IS NOT NULL AND t.year IS NOT NULL
    ),
    Cumulative AS (
        SELECT id,
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
    SET t.Cumulative_Sched = c.CumSched,
        t.Cumulative_Achieved = c.CumAchieved
    FROM dbo.ipnwmtcdata t
    INNER JOIN Cumulative c ON t.id = c.id;

    PRINT 'IPNW: ' + CAST(@@ROWCOUNT AS varchar) + ' rows updated';
END;

-- ============================================================
-- SLBN (cycleSize = 2)
-- ============================================================
IF OBJECT_ID(N'dbo.slbnmtcdata', N'U') IS NOT NULL
BEGIN
    WITH MonthMap AS (
        SELECT v.MonthName, v.MonthNum FROM (VALUES
            ('January',1),('February',2),('March',3),('April',4),
            ('May',5),('June',6),('July',7),('August',8),
            ('September',9),('October',10),('November',11),('December',12)
        ) v(MonthName, MonthNum)
    ),
    Numbered AS (
        SELECT t.id, t.designation, t.year, mm.MonthNum,
            ((mm.MonthNum - 1) / 2) + 1 AS CycleNum,
            t.scheduled, t.attended
        FROM dbo.slbnmtcdata t
        INNER JOIN MonthMap mm ON LTRIM(RTRIM(mm.MonthName)) = LTRIM(RTRIM(t.month))
        WHERE t.designation IS NOT NULL AND t.year IS NOT NULL
    ),
    Cumulative AS (
        SELECT id,
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
    SET t.Cumulative_Sched = c.CumSched,
        t.Cumulative_Achieved = c.CumAchieved
    FROM dbo.slbnmtcdata t
    INNER JOIN Cumulative c ON t.id = c.id;

    PRINT 'SLBN: ' + CAST(@@ROWCOUNT AS varchar) + ' rows updated';
END;
