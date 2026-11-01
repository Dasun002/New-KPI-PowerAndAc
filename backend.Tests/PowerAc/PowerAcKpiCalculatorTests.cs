using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Xunit;

namespace backend.Tests.PowerAc
{
    public class PowerAcKpiCalculatorTests
    {
        private const int PM = 1, SAFETY = 2, CORRECTIVE = 3, AVAILABILITY = 4;

        private static List<PowerAcComponentConfig> Components(
            decimal pmWeight = 25, decimal safetyWeight = 25, decimal corrWeight = 25, decimal availWeight = 25,
            string safetyMethod = PowerAcScoringMethods.ZeroIncidentsBinary, bool safetyApproved = true,
            string pmMethod = PowerAcScoringMethods.RatioCapped, string availMethod = PowerAcScoringMethods.DirectPercent,
            decimal? safetyParameter = null)
        {
            return new List<PowerAcComponentConfig>
            {
                new(PM, PowerAcComponentCodes.PreventiveMaintenance, "Preventive Maintenance", 100m, pmMethod, null, true, pmWeight),
                new(SAFETY, PowerAcComponentCodes.CriticalSafety, "Critical Safety", 0m, safetyMethod, safetyParameter, safetyApproved, safetyWeight),
                new(CORRECTIVE, PowerAcComponentCodes.CorrectiveAction, "Corrective Action", 100m, PowerAcScoringMethods.RatioCapped, null, true, corrWeight),
                new(AVAILABILITY, PowerAcComponentCodes.PowerSystemAvailability, "Power System Availability", 99m, availMethod, null, true, availWeight)
            };
        }

        // Workbook 'Power & AC'!G6:G9 for NW/WPC1: PM 0.9, Safety 1, Corrective 0.8, Availability 1.
        private static Dictionary<int, PowerAcMeasurement> WorkbookMeasurements(bool verified = true) => new()
        {
            [PM] = new PowerAcMeasurement { ApplicableCount = 10, CompletedCount = 9, Verified = verified },
            [SAFETY] = new PowerAcMeasurement { IncidentCount = 0, Verified = verified },
            [CORRECTIVE] = new PowerAcMeasurement { ApplicableCount = 10, CompletedCount = 8, Verified = verified },
            [AVAILABILITY] = new PowerAcMeasurement { AchievementPercent = 100, Verified = verified }
        };

        private static PowerAcGroupInput Group(int id, decimal? allocation, Dictionary<int, PowerAcMeasurement>? m = null, bool mapped = true) => new()
        {
            EngineerGroupId = id,
            Code = $"G{id}",
            MappingVerified = mapped,
            AllocationValue = allocation,
            Measurements = m ?? WorkbookMeasurements()
        };

        private static PowerAcCalculationSettings Settings(
            string method = PowerAcPointsMethods.TargetScaled, bool approved = true, bool requireVerified = true, decimal? points = 5000m) => new()
        {
            PointsApplicable = points,
            PointsMethod = method,
            CompositeTarget = 1m,
            PointsMethodApproved = approved,
            RequireVerifiedData = requireVerified
        };

        [Fact]
        public void Reproduces_workbook_composite_and_points()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 7m), Group(2, 3m) }, Settings());

            var g1 = output.Groups[0];
            Assert.True(output.WeightsValid);
            Assert.True(output.AllocationsComplete);
            Assert.Equal(PowerAcResultStatus.Complete, g1.Status);
            Assert.Equal(0.925m, g1.CompositeAchievement);
            // J19 = 5000 x 7 / 10 ; K19 = IF(0.925 > 1, J19, J19 x 0.925 / 1)
            Assert.Equal(3500m, g1.MaximumPoints);
            Assert.Equal(3237.5m, g1.PointsAchieved);
            Assert.Equal(1500m, output.Groups[1].MaximumPoints);
        }

        [Fact]
        public void Uses_configured_weights_not_fixed_25_percent()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(40, 20, 20, 20), new[] { Group(1, 1m) }, Settings());
            // 0.9x0.4 + 1x0.2 + 0.8x0.2 + 1x0.2 = 0.92
            Assert.Equal(0.92m, output.Groups[0].CompositeAchievement);
        }

        [Fact]
        public void Weights_not_totalling_100_block_the_score()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(30, 25, 25, 25), new[] { Group(1, 1m) }, Settings());

            Assert.False(output.WeightsValid);
            Assert.Equal(105m, output.WeightTotal);
            Assert.Contains(output.GlobalIssues, i => i.Contains("must total 100%"));
            Assert.Null(output.Groups[0].CompositeAchievement);
            Assert.Null(output.Groups[0].PointsAchieved);
            Assert.Equal(PowerAcResultStatus.Incomplete, output.Groups[0].Status);
        }

        [Fact]
        public void Missing_weight_is_reported()
        {
            var components = Components();
            components[3] = components[3] with { WeightPercent = null };
            var output = PowerAcKpiCalculator.Calculate(components, new[] { Group(1, 1m) }, Settings());

            Assert.False(output.WeightsValid);
            Assert.Contains(output.GlobalIssues, i => i.Contains("No weight is configured for component AVAILABILITY"));
        }

        [Fact]
        public void Missing_allocation_for_any_group_blocks_points_for_all_groups()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 5m), Group(2, null) }, Settings());

            Assert.False(output.AllocationsComplete);
            Assert.Null(output.TotalAllocation);
            Assert.All(output.Groups, g =>
            {
                Assert.Null(g.MaximumPoints);
                Assert.Null(g.PointsAchieved);
                Assert.Equal(PowerAcResultStatus.Incomplete, g.Status);
            });
            // The composite itself is still shown for the group with complete data.
            Assert.Equal(0.925m, output.Groups[0].CompositeAchievement);
            Assert.Contains(output.GlobalIssues, i => i.Contains("G2"));
        }

        [Fact]
        public void Zero_total_allocation_is_rejected()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 0m), Group(2, 0m) }, Settings());

            Assert.False(output.AllocationsComplete);
            Assert.Contains(output.GlobalIssues, i => i.Contains("Total area allocation is zero"));
            Assert.All(output.Groups, g => Assert.Null(g.MaximumPoints));
        }

        [Fact]
        public void Group_with_zero_allocation_gets_zero_maximum_points_when_total_is_positive()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 0m), Group(2, 4m) }, Settings());
            Assert.Equal(0m, output.Groups[0].MaximumPoints);
            Assert.Equal(5000m, output.Groups[1].MaximumPoints);
        }

        [Fact]
        public void Missing_component_data_is_not_treated_as_zero()
        {
            var m = WorkbookMeasurements();
            m.Remove(CORRECTIVE);
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m, m) }, Settings());

            var g = output.Groups[0];
            Assert.Equal(PowerAcResultStatus.Incomplete, g.Status);
            Assert.Null(g.CompositeAchievement);
            Assert.Null(g.PointsAchieved);
            var corrective = g.Components.Single(c => c.ComponentId == CORRECTIVE);
            Assert.Equal(PowerAcResultStatus.Incomplete, corrective.Status);
            Assert.Null(corrective.Score);
        }

        [Fact]
        public void Critical_safety_pending_formula_gives_pending_status_and_no_points()
        {
            var output = PowerAcKpiCalculator.Calculate(
                Components(safetyMethod: PowerAcScoringMethods.Pending, safetyApproved: false),
                new[] { Group(1, 1m) }, Settings());

            var g = output.Groups[0];
            Assert.Equal(PowerAcResultStatus.Pending, g.Status);
            Assert.Null(g.CompositeAchievement);
            Assert.Null(g.PointsAchieved);
            Assert.Equal(5000m, g.MaximumPoints);
            Assert.Contains(g.Issues, i => i.Contains("pending business confirmation"));
        }

        [Fact]
        public void Unapproved_scoring_method_is_not_scored()
        {
            var output = PowerAcKpiCalculator.Calculate(
                Components(safetyApproved: false), new[] { Group(1, 1m) }, Settings());
            Assert.Equal(PowerAcResultStatus.Pending, output.Groups[0].Status);
            Assert.Null(output.Groups[0].Components.Single(c => c.ComponentId == SAFETY).Score);
        }

        [Theory]
        [InlineData(0, 1.0)]
        [InlineData(1, 0.0)]
        [InlineData(3, 0.0)]
        public void Zero_incident_binary_rule_when_approved(int incidents, double expected)
        {
            var m = WorkbookMeasurements();
            m[SAFETY] = new PowerAcMeasurement { IncidentCount = incidents, Verified = true };
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m, m) }, Settings());
            Assert.Equal((decimal)expected, output.Groups[0].Components.Single(c => c.ComponentId == SAFETY).Score);
        }

        [Theory]
        [InlineData(0, 1.0)]
        [InlineData(1, 0.75)]
        [InlineData(5, 0.0)]
        public void Deduction_per_incident_rule_when_approved(int incidents, double expected)
        {
            var m = WorkbookMeasurements();
            m[SAFETY] = new PowerAcMeasurement { IncidentCount = incidents, Verified = true };
            var output = PowerAcKpiCalculator.Calculate(
                Components(safetyMethod: PowerAcScoringMethods.DeductionPerIncident, safetyParameter: 25m),
                new[] { Group(1, 1m, m) }, Settings());
            Assert.Equal((decimal)expected, output.Groups[0].Components.Single(c => c.ComponentId == SAFETY).Score);
        }

        [Fact]
        public void Corrective_action_ratio_is_completed_over_applicable()
        {
            var m = WorkbookMeasurements();
            m[CORRECTIVE] = new PowerAcMeasurement { ApplicableCount = 40, CompletedCount = 30, Verified = true };
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m, m) }, Settings());
            var c = output.Groups[0].Components.Single(x => x.ComponentId == CORRECTIVE);
            Assert.Equal(0.75m, c.AchievementRatio);
            Assert.Equal(0.75m, c.Score);
            Assert.Equal(0.1875m, c.WeightedScore);
        }

        [Fact]
        public void Availability_from_site_counts_and_from_entered_percentage()
        {
            var m = WorkbookMeasurements();
            m[AVAILABILITY] = new PowerAcMeasurement { ApplicableCount = 200, CompletedCount = 198, Verified = true };
            var byCount = PowerAcKpiCalculator.Calculate(Components(availMethod: PowerAcScoringMethods.RatioCapped), new[] { Group(1, 1m, m) }, Settings());
            Assert.Equal(0.99m, byCount.Groups[0].Components.Single(x => x.ComponentId == AVAILABILITY).Score);

            // TargetScaled against the 99% target: 0.99 / 0.99 = 1
            var scaled = PowerAcKpiCalculator.Calculate(Components(availMethod: PowerAcScoringMethods.TargetScaled), new[] { Group(1, 1m, m) }, Settings());
            Assert.Equal(1m, scaled.Groups[0].Components.Single(x => x.ComponentId == AVAILABILITY).Score);

            var m2 = WorkbookMeasurements();
            m2[AVAILABILITY] = new PowerAcMeasurement { AchievementPercent = 97.5m, Verified = true };
            var direct = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m, m2) }, Settings());
            Assert.Equal(0.975m, direct.Groups[0].Components.Single(x => x.ComponentId == AVAILABILITY).Score);
        }

        [Fact]
        public void Over_achievement_is_capped_with_a_warning_under_RatioCapped()
        {
            var m = WorkbookMeasurements();
            m[PM] = new PowerAcMeasurement { ApplicableCount = 13, CompletedCount = 14, Verified = true };
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m, m) }, Settings());
            var pm = output.Groups[0].Components.Single(x => x.ComponentId == PM);
            Assert.Equal(1m, pm.Score);
            Assert.Contains(output.Groups[0].Warnings, w => w.Contains("capped"));
            Assert.Equal(PowerAcResultStatus.Complete, output.Groups[0].Status);
        }

        [Fact]
        public void Over_achievement_is_rejected_under_RatioStrict()
        {
            var m = WorkbookMeasurements();
            m[PM] = new PowerAcMeasurement { ApplicableCount = 13, CompletedCount = 14, Verified = true };
            var output = PowerAcKpiCalculator.Calculate(Components(pmMethod: PowerAcScoringMethods.RatioStrict), new[] { Group(1, 1m, m) }, Settings());
            Assert.Equal(PowerAcResultStatus.Incomplete, output.Groups[0].Status);
            Assert.Contains(output.Groups[0].Issues, i => i.Contains("RatioStrict"));
        }

        [Fact]
        public void Zero_applicable_records_make_the_ratio_undefined()
        {
            var m = WorkbookMeasurements();
            m[PM] = new PowerAcMeasurement { ApplicableCount = 0, CompletedCount = 0, Verified = true };
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m, m) }, Settings());
            Assert.Equal(PowerAcResultStatus.Incomplete, output.Groups[0].Status);
            Assert.Contains(output.Groups[0].Issues, i => i.Contains("undefined"));
        }

        [Fact]
        public void Unverified_data_blocks_the_score_when_verification_is_required()
        {
            var groups = new[] { Group(1, 1m, WorkbookMeasurements(verified: false)) };

            var strict = PowerAcKpiCalculator.Calculate(Components(), groups, Settings(requireVerified: true));
            Assert.Equal(PowerAcResultStatus.Incomplete, strict.Groups[0].Status);

            var relaxed = PowerAcKpiCalculator.Calculate(Components(), groups, Settings(requireVerified: false));
            Assert.Equal(PowerAcResultStatus.Complete, relaxed.Groups[0].Status);
        }

        [Fact]
        public void Unverified_engineer_group_mapping_blocks_the_result()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m, mapped: false) }, Settings());
            Assert.Equal(PowerAcResultStatus.Incomplete, output.Groups[0].Status);
            Assert.Null(output.Groups[0].PointsAchieved);
        }

        [Fact]
        public void Unapproved_points_formula_gives_pending()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m) }, Settings(approved: false));
            Assert.Equal(PowerAcResultStatus.Pending, output.Groups[0].Status);
            Assert.Null(output.Groups[0].PointsAchieved);
            Assert.Equal(0.925m, output.Groups[0].CompositeAchievement);
        }

        [Fact]
        public void Points_applicable_is_split_by_allocation_share()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m), Group(2, 2m), Group(3, 3m) }, Settings(points: 6000m));
            Assert.Equal(new[] { 1000m, 2000m, 3000m }, output.Groups.Select(g => g.MaximumPoints!.Value).ToArray());
            Assert.Equal(6000m, output.Groups.Sum(g => g.MaximumPoints!.Value));
        }

        [Fact]
        public void Points_formulas()
        {
            var settings = Settings();
            Assert.Equal(100m, PowerAcKpiCalculator.CalculatePointsAchieved(100m, 1.2m, settings, out _));
            Assert.Equal(92.5m, PowerAcKpiCalculator.CalculatePointsAchieved(100m, 0.925m, settings, out _));
            Assert.Equal(92.5m, PowerAcKpiCalculator.CalculatePointsAchieved(100m, 0.925m, Settings(method: PowerAcPointsMethods.Linear), out _));
            Assert.Null(PowerAcKpiCalculator.CalculatePointsAchieved(100m, 0.9m, Settings(method: "Unknown"), out var issue));
            Assert.NotNull(issue);
        }

        [Fact]
        public void Missing_points_applicable_blocks_points()
        {
            var output = PowerAcKpiCalculator.Calculate(Components(), new[] { Group(1, 1m) }, Settings(points: null));
            Assert.Null(output.Groups[0].MaximumPoints);
            Assert.Equal(PowerAcResultStatus.Incomplete, output.Groups[0].Status);
            Assert.Contains(output.GlobalIssues, i => i.Contains("Points Applicable"));
        }
    }
}
