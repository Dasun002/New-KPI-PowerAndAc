using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Xunit;

namespace backend.Tests.PowerAc
{
    public class PowerAcCalculatorTests
    {
        private static readonly IReadOnlyList<PowerAcConfig.Component> Components = new[]
        {
            new PowerAcConfig.Component(1, "PM", "Preventive Maintenance", 25m, "100 %", 100m),
            new PowerAcConfig.Component(2, "SAFETY", "Critical Safety", 25m, "0 Incidents", 0m),
            new PowerAcConfig.Component(3, "CORRECTIVE", "Corrective Action and Maintenance Record", 25m, "100 %", 100m),
            new PowerAcConfig.Component(4, "AVAILABILITY", "Power System Availability", 25m, "99 %", 99m),
        };

        private static List<PowerAcPmData> FullMonth(decimal pm = 90m, decimal safety = 100m, decimal corrective = 100m, decimal availability = 100m, decimal allocation = 100m, bool verified = true)
        {
            return PowerAcConfig.Groups.Select(g => new PowerAcPmData
            {
                Designation = g.Designation,
                Year = "2026",
                Month = "November",
                PmAchievement = pm,
                SafetyAchievement = safety,
                CorrectiveAchievement = corrective,
                AvailabilityAchievement = availability,
                AreaAllocation = allocation,
                IsVerified = verified
            }).ToList();
        }

        [Fact]
        public void Full_verified_month_gives_the_expected_composite_and_points()
        {
            // Composite 0.9x0.25 + 1x0.25 + 1x0.25 + 1x0.25 = 0.975. Each group gets 5000 x 100 / 2000 = 250 maximum points.
            var results = PowerAcCalculator.Calculate(FullMonth(), Components, 5000m);
            Assert.All(results, r => Assert.Equal("Complete", r.Status));
            Assert.All(results, r => Assert.Equal(0.975m, r.Composite));
            Assert.All(results, r => Assert.Equal(250m, r.MaximumPoints));
            Assert.All(results, r => Assert.Equal(243.75m, r.PointsAchieved));
        }

        [Fact]
        public void Missing_achievement_makes_the_group_incomplete_and_is_never_treated_as_zero()
        {
            var rows = FullMonth();
            rows[0].CorrectiveAchievement = null;
            var results = PowerAcCalculator.Calculate(rows, Components, 5000m);
            var first = results[0];
            Assert.Equal("Incomplete", first.Status);
            Assert.Null(first.Composite);
            Assert.Null(first.PointsAchieved);
            Assert.Contains(first.Issues, i => i.Contains("Corrective"));
        }

        [Fact]
        public void Unverified_data_gives_no_points()
        {
            var results = PowerAcCalculator.Calculate(FullMonth(verified: false), Components, 5000m);
            Assert.All(results, r => Assert.Equal("Incomplete", r.Status));
            Assert.All(results, r => Assert.Null(r.PointsAchieved));
        }

        [Fact]
        public void Points_need_all_twenty_allocations()
        {
            var rows = FullMonth();
            rows[5].AreaAllocation = null;
            var results = PowerAcCalculator.Calculate(rows, Components, 5000m);
            Assert.All(results, r => Assert.Null(r.PointsAchieved));
            Assert.Contains(results[0].Issues, i => i.Contains("20 area allocations"));
        }
    }
}
