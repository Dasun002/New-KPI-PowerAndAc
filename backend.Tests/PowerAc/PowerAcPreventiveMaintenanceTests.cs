using backend.Models;
using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Xunit;

namespace backend.Tests.PowerAc
{
    public class PowerAcPreventiveMaintenanceTests
    {
        private static readonly PowerAcEngineerGroup Group = new()
        {
            Id = 1, Code = "CPN", PmDesignation = "NW/CPN", MappingVerified = true, RegionDataId = 11
        };

        // Mirrors the real NW/CPN rows: cumulative values reset in April (start of Q2).
        private static List<PowerAndAC> CpnRows(bool verified = true) => new()
        {
            Row(1, 13, 13, 13, 13, verified),
            Row(2, 13, 14, 26, 27, verified),
            Row(3, 13, 14, 39, 41, verified),
            Row(4, 13, 13, 13, 13, verified),
            Row(5, 13, 0, 26, 13, verified),
            Row(6, 13, 0, 39, 13, verified)
        };

        private static PowerAndAC Row(int month, int sched, int att, int cumS, int cumA, bool verified = true) => new()
        {
            Id = month, Designation = "NW/CPN", Year = 2026, Month = month,
            Scheduled = sched, Attended = att, Cumulative_Sched = cumS, Cumulative_Achieved = cumA, IsVerified = verified
        };

        private static PowerAcPeriod Period(string type, int n) =>
            PowerAcPeriod.TryCreate(2026, type, n, out var p, out _) ? p! : throw new InvalidOperationException();

        [Fact]
        public void Quarter_one_sums_january_to_march_monthly_values()
        {
            var m = PowerAcKpiCalculationService.BuildPmMeasurement(Period("Quarter", 1), Group, CpnRows())!;
            Assert.True(m.DataComplete);
            Assert.Equal(39m, m.ApplicableCount);
            Assert.Equal(41m, m.CompletedCount);
        }

        [Fact]
        public void Quarter_two_starts_fresh_in_april()
        {
            var m = PowerAcKpiCalculationService.BuildPmMeasurement(Period("Quarter", 2), Group, CpnRows())!;
            Assert.Equal(39m, m.ApplicableCount);
            Assert.Equal(13m, m.CompletedCount);
            // The stored June cumulative snapshot (39/13) matches, so there is no integrity warning.
            Assert.DoesNotContain(m.Warnings, w => w.Contains("differ"));
        }

        [Fact]
        public void April_reset_is_not_treated_as_an_error()
        {
            var m = PowerAcKpiCalculationService.BuildPmMeasurement(Period("Month", 4), Group, CpnRows())!;
            Assert.True(m.DataComplete);
            Assert.Empty(m.Issues);
            Assert.Equal(13m, m.ApplicableCount); // April only, not Jan-Apr
            Assert.Equal(13m, m.CompletedCount);
        }

        [Fact]
        public void Monthly_result_uses_quarter_to_date_monthly_values()
        {
            var m = PowerAcKpiCalculationService.BuildPmMeasurement(Period("Month", 5), Group, CpnRows())!;
            Assert.Equal(26m, m.ApplicableCount); // April + May
            Assert.Equal(13m, m.CompletedCount);
        }

        [Fact]
        public void Cumulative_snapshots_are_never_summed()
        {
            // Corrupt the cumulative columns. The measurement must still come from Scheduled/Attended.
            var rows = CpnRows();
            rows.ForEach(r => { r.Cumulative_Sched = 999; r.Cumulative_Achieved = 999; });
            var m = PowerAcKpiCalculationService.BuildPmMeasurement(Period("Quarter", 2), Group, rows)!;
            Assert.Equal(39m, m.ApplicableCount);
            Assert.Equal(13m, m.CompletedCount);
            Assert.Contains(m.Warnings, w => w.Contains("differ from the stored cumulative snapshot"));
        }

        [Fact]
        public void Missing_month_makes_the_quarter_incomplete()
        {
            var rows = CpnRows().Where(r => r.Month != 5).ToList();
            var m = PowerAcKpiCalculationService.BuildPmMeasurement(Period("Quarter", 2), Group, rows)!;
            Assert.False(m.DataComplete);
            Assert.Contains(m.Issues, i => i.Contains("May"));
        }

        [Fact]
        public void Attended_above_scheduled_is_reported_and_source_is_unchanged()
        {
            var rows = CpnRows();
            var m = PowerAcKpiCalculationService.BuildPmMeasurement(Period("Quarter", 1), Group, rows)!;
            Assert.Contains(m.Warnings, w => w.Contains("exceeds Scheduled"));
            Assert.Equal(14, rows.Single(r => r.Month == 2).Attended);
        }

        [Fact]
        public void Verification_reflects_every_row_in_the_period()
        {
            var rows = CpnRows();
            Assert.True(PowerAcKpiCalculationService.BuildPmMeasurement(Period("Quarter", 1), Group, rows)!.Verified);
            rows.Single(r => r.Month == 2).IsVerified = false;
            Assert.False(PowerAcKpiCalculationService.BuildPmMeasurement(Period("Quarter", 1), Group, rows)!.Verified);
        }

        [Fact]
        public void Group_without_pm_designation_is_incomplete()
        {
            var unmapped = new PowerAcEngineerGroup { Id = 2, Code = "EP" };
            var m = PowerAcKpiCalculationService.BuildPmMeasurement(Period("Month", 1), unmapped, CpnRows())!;
            Assert.False(m.DataComplete);
        }

        [Theory]
        [InlineData("Month", 1, 1, new byte[] { 1 })]
        [InlineData("Month", 3, 3, new byte[] { 1, 2, 3 })]
        [InlineData("Month", 4, 4, new byte[] { 4 })]
        [InlineData("Month", 6, 6, new byte[] { 4, 5, 6 })]
        [InlineData("Quarter", 2, 6, new byte[] { 4, 5, 6 })]
        [InlineData("Quarter", 4, 12, new byte[] { 10, 11, 12 })]
        public void Period_months(string type, int n, int endMonth, byte[] pmMonths)
        {
            var p = Period(type, n);
            Assert.Equal(endMonth, p.EndMonth);
            Assert.Equal(pmMonths, p.PmMonths.ToArray());
        }

        [Theory]
        [InlineData(2026, "Month", 13)]
        [InlineData(2026, "Quarter", 5)]
        [InlineData(2026, "Week", 1)]
        [InlineData(1999, "Month", 1)]
        public void Invalid_periods_are_rejected(int year, string type, int n)
        {
            Assert.False(PowerAcPeriod.TryCreate(year, type, n, out _, out var error));
            Assert.NotNull(error);
        }
    }
}
