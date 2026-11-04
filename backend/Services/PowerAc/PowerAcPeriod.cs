/*
 * File: PowerAcPeriod.cs
 * Reporting period rules for the Power & AC KPI.
 * Quarters are calendar quarters: Q1 = Jan-Mar, Q2 = Apr-Jun, Q3 = Jul-Sep, Q4 = Oct-Dec.
 * The PowerAndAC cumulative columns reset at the start of each quarter (for example in April); that is expected.
 */
using backend.Models.PowerAc;

namespace backend.Services.PowerAc
{
    public sealed record PowerAcPeriod(short Year, string PeriodType, byte PeriodNumber)
    {
        public static bool TryCreate(int year, string? periodType, int periodNumber, out PowerAcPeriod? period, out string? error)
        {
            period = null;
            error = null;
            var type = string.Equals(periodType, PowerAcPeriodTypes.Quarter, StringComparison.OrdinalIgnoreCase)
                ? PowerAcPeriodTypes.Quarter
                : string.Equals(periodType, PowerAcPeriodTypes.Month, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(periodType)
                    ? PowerAcPeriodTypes.Month
                    : null;

            if (type is null) { error = "periodType must be Month or Quarter."; return false; }
            if (year < 2000 || year > 9999) { error = "year must be between 2000 and 9999."; return false; }
            var max = type == PowerAcPeriodTypes.Month ? 12 : 4;
            if (periodNumber < 1 || periodNumber > max) { error = $"period must be between 1 and {max} for {type}."; return false; }

            period = new PowerAcPeriod((short)year, type, (byte)periodNumber);
            return true;
        }

        public static int QuarterOf(int month) => ((month - 1) / 3) + 1;

        // Last month of the period. Effective-dated configuration is resolved against this month.
        public byte EndMonth => PeriodType == PowerAcPeriodTypes.Quarter ? (byte)(PeriodNumber * 3) : PeriodNumber;

        // Months whose own source records are needed for non-PM components.
        public IReadOnlyList<byte> SourceMonths => PeriodType == PowerAcPeriodTypes.Quarter
            ? new[] { (byte)(PeriodNumber * 3 - 2), (byte)(PeriodNumber * 3 - 1), (byte)(PeriodNumber * 3) }
            : new[] { PeriodNumber };

        // Months of PM data needed. A monthly result uses quarter-to-date data (the existing PowerAndAC convention);
        // a quarterly result needs all three months of the quarter.
        public IReadOnlyList<byte> PmMonths
        {
            get
            {
                var quarter = PeriodType == PowerAcPeriodTypes.Quarter ? PeriodNumber : QuarterOf(PeriodNumber);
                var start = (byte)(quarter * 3 - 2);
                return Enumerable.Range(start, EndMonth - start + 1).Select(m => (byte)m).ToList();
            }
        }

        public int EndKey => Year * 100 + EndMonth;

        public static int Key(int year, int month) => year * 100 + month;

        public override string ToString() => PeriodType == PowerAcPeriodTypes.Quarter
            ? $"Q{PeriodNumber} {Year}"
            : $"{Year}-{PeriodNumber:00}";
    }
}
