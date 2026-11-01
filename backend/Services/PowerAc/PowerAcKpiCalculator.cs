/*
 * File: PowerAcKpiCalculator.cs
 * Pure calculation engine for the four-component Power & AC KPI. It has no database access.
 *
 * Formulas (workbook "New KPI Calculation_November 2026_New Method-1 (3).xlsx"):
 *   Composite achievement   = sum(component score x component weight / 100)       ('Power & AC'!G11, weights configurable)
 *   Maximum points per KPI  = PointsApplicable x group allocation / total allocation ('KPI Calculation'!J19)
 *   Points achieved         = IF(composite > target, max, max x composite / target)  ('KPI Calculation'!K19, target 1)
 *
 * Missing data is never treated as zero. A group only gets points when every weighted component is scored,
 * every scoring method is approved, the allocation set is complete and the points formula is approved.
 */
using backend.Models.PowerAc;

namespace backend.Services.PowerAc
{
    public sealed record PowerAcComponentConfig(
        int ComponentId,
        string Code,
        string Name,
        decimal TargetValue,
        string ScoringMethod,
        decimal? ScoringParameter,
        bool ScoringApproved,
        decimal? WeightPercent,
        // Scoring version used (null when no version is effective for the period).
        int? VersionId = null,
        // Set when the component cannot be scored for a configuration reason (for example no version).
        string? UnavailableReason = null);

    public sealed class PowerAcMeasurement
    {
        public decimal? ApplicableCount { get; init; }
        public decimal? CompletedCount { get; init; }
        public int? IncidentCount { get; init; }
        public decimal? AchievementPercent { get; init; }
        // False when a required month of source data is missing.
        public bool DataComplete { get; init; } = true;
        public bool Verified { get; init; }
        public List<string> Issues { get; init; } = new();
        public List<string> Warnings { get; init; } = new();
    }

    public sealed class PowerAcGroupInput
    {
        public int EngineerGroupId { get; init; }
        public string Code { get; init; } = string.Empty;
        public bool MappingVerified { get; init; }
        public decimal? AllocationValue { get; init; }
        public Dictionary<int, PowerAcMeasurement> Measurements { get; init; } = new();
    }

    public sealed class PowerAcCalculationSettings
    {
        public decimal? PointsApplicable { get; init; }
        public string PointsMethod { get; init; } = PowerAcPointsMethods.TargetScaled;
        public decimal CompositeTarget { get; init; } = 1m;
        public bool PointsMethodApproved { get; init; }
        public bool RequireVerifiedData { get; init; } = true;
        // Problems found while resolving settings (for example, no settings row for the period).
        public List<string> Issues { get; init; } = new();
    }

    public sealed class PowerAcComponentScoreResult
    {
        public int ComponentId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public decimal? WeightPercent { get; init; }
        public string ScoringMethod { get; init; } = string.Empty;
        public decimal? ApplicableCount { get; set; }
        public decimal? CompletedCount { get; set; }
        public int? IncidentCount { get; set; }
        public decimal? AchievementRatio { get; set; }
        public decimal? Score { get; set; }
        public decimal? WeightedScore { get; set; }
        public string Status { get; set; } = PowerAcResultStatus.Incomplete;
        public string? Message { get; set; }
    }

    public sealed class PowerAcGroupResult
    {
        public int EngineerGroupId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Status { get; set; } = PowerAcResultStatus.Incomplete;
        public decimal? CompositeAchievement { get; set; }
        public decimal? PointsApplicable { get; set; }
        public decimal? AllocationValue { get; set; }
        public decimal? TotalAllocation { get; set; }
        public decimal? MaximumPoints { get; set; }
        public decimal? PointsAchieved { get; set; }
        public List<string> Issues { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<PowerAcComponentScoreResult> Components { get; } = new();
    }

    public sealed class PowerAcCalculationOutput
    {
        public List<string> GlobalIssues { get; } = new();
        public bool WeightsValid { get; set; }
        public decimal WeightTotal { get; set; }
        public bool AllocationsComplete { get; set; }
        public decimal? TotalAllocation { get; set; }
        public List<PowerAcGroupResult> Groups { get; } = new();
    }

    public static class PowerAcKpiCalculator
    {
        private const decimal WeightTolerance = 0.0001m;

        public static PowerAcCalculationOutput Calculate(
            IReadOnlyList<PowerAcComponentConfig> components,
            IReadOnlyList<PowerAcGroupInput> groups,
            PowerAcCalculationSettings settings)
        {
            var output = new PowerAcCalculationOutput();
            output.GlobalIssues.AddRange(settings.Issues);

            // 1. Weights: every active component needs a weight, none negative, total exactly 100%.
            var weightIssues = ValidateWeights(components, out var weightTotal);
            output.WeightTotal = weightTotal;
            output.WeightsValid = weightIssues.Count == 0;
            output.GlobalIssues.AddRange(weightIssues);

            // 2. Allocations: every group needs a value >= 0 and the total must be above zero.
            var allocationIssues = ValidateAllocations(groups, out var totalAllocation);
            output.AllocationsComplete = allocationIssues.Count == 0;
            output.TotalAllocation = output.AllocationsComplete ? totalAllocation : null;
            output.GlobalIssues.AddRange(allocationIssues);

            if (settings.PointsApplicable is null)
                output.GlobalIssues.Add("Points Applicable is not available for this period.");
            else if (settings.PointsApplicable < 0)
                output.GlobalIssues.Add("Points Applicable cannot be negative.");

            foreach (var group in groups)
                output.Groups.Add(CalculateGroup(group, components, settings, output));

            return output;
        }

        private static List<string> ValidateWeights(IReadOnlyList<PowerAcComponentConfig> components, out decimal total)
        {
            var issues = new List<string>();
            total = 0m;
            if (components.Count == 0)
            {
                issues.Add("No active Power & AC components are configured.");
                return issues;
            }

            foreach (var c in components)
            {
                if (c.WeightPercent is null)
                {
                    issues.Add($"No weight is configured for component {c.Code} for this period.");
                    continue;
                }
                if (c.WeightPercent < 0m || c.WeightPercent > 100m)
                    issues.Add($"Weight for component {c.Code} must be between 0 and 100.");
                total += c.WeightPercent.Value;
            }

            if (issues.Count == 0 && Math.Abs(total - 100m) > WeightTolerance)
                issues.Add($"Active component weights total {total:0.####}%. They must total 100%.");

            return issues;
        }

        private static List<string> ValidateAllocations(IReadOnlyList<PowerAcGroupInput> groups, out decimal total)
        {
            var issues = new List<string>();
            total = 0m;
            if (groups.Count == 0)
            {
                issues.Add("No active engineer groups are configured.");
                return issues;
            }

            var missing = groups.Where(g => g.AllocationValue is null).Select(g => g.Code).ToList();
            if (missing.Count > 0)
                issues.Add($"Area allocation is missing for {missing.Count} engineer group(s): {string.Join(", ", missing)}. Points are not calculated until all allocations are entered.");

            var negative = groups.Where(g => g.AllocationValue < 0m).Select(g => g.Code).ToList();
            if (negative.Count > 0)
                issues.Add($"Area allocation is negative for: {string.Join(", ", negative)}.");

            total = groups.Sum(g => g.AllocationValue ?? 0m);
            if (missing.Count == 0 && negative.Count == 0 && total <= 0m)
                issues.Add("Total area allocation is zero. Points cannot be allocated.");

            return issues;
        }

        private static PowerAcGroupResult CalculateGroup(
            PowerAcGroupInput group,
            IReadOnlyList<PowerAcComponentConfig> components,
            PowerAcCalculationSettings settings,
            PowerAcCalculationOutput output)
        {
            var result = new PowerAcGroupResult
            {
                EngineerGroupId = group.EngineerGroupId,
                Code = group.Code,
                AllocationValue = group.AllocationValue,
                TotalAllocation = output.TotalAllocation,
                PointsApplicable = settings.PointsApplicable
            };

            if (!group.MappingVerified)
                result.Issues.Add("Engineer group mapping to regiondata is not verified.");

            foreach (var component in components)
            {
                group.Measurements.TryGetValue(component.ComponentId, out var measurement);
                var score = ScoreComponent(component, measurement, settings.RequireVerifiedData);
                result.Components.Add(score);
                if (measurement != null)
                    result.Warnings.AddRange(measurement.Warnings.Select(w => $"{component.Code}: {w}"));
                if (score.Message != null)
                {
                    if (score.Status == PowerAcResultStatus.Complete)
                        result.Warnings.Add($"{component.Code}: {score.Message}");
                    else
                        result.Issues.Add($"{component.Code}: {score.Message}");
                }
            }

            var allScored = output.WeightsValid
                && result.Components.Count > 0
                && result.Components.All(c => c.Status == PowerAcResultStatus.Complete);

            if (allScored)
                result.CompositeAchievement = Math.Round(result.Components.Sum(c => c.WeightedScore ?? 0m), 6);

            // Maximum points depend only on the allocation set, so they are shown even when the score is incomplete.
            if (output.AllocationsComplete && settings.PointsApplicable is decimal pointsApplicable && pointsApplicable >= 0m
                && group.AllocationValue is decimal allocation && output.TotalAllocation is decimal total && total > 0m)
            {
                result.MaximumPoints = Math.Round(pointsApplicable * allocation / total, 4);
            }

            var anyIncomplete = !group.MappingVerified
                || !output.WeightsValid
                || result.Components.Any(c => c.Status == PowerAcResultStatus.Incomplete)
                || result.MaximumPoints is null;
            var anyPending = result.Components.Any(c => c.Status == PowerAcResultStatus.Pending)
                || !settings.PointsMethodApproved;

            if (!settings.PointsMethodApproved)
                result.Issues.Add("The points formula is not approved.");
            if (result.MaximumPoints is null && output.AllocationsComplete && settings.PointsApplicable is not null)
                result.Issues.Add("Maximum points could not be calculated.");
            else if (!output.AllocationsComplete)
                result.Issues.Add("Points are not calculated because the area allocation set is incomplete.");

            if (anyIncomplete)
                result.Status = PowerAcResultStatus.Incomplete;
            else if (anyPending)
                result.Status = PowerAcResultStatus.Pending;
            else
            {
                var points = CalculatePointsAchieved(result.MaximumPoints!.Value, result.CompositeAchievement!.Value, settings, out var pointsIssue);
                if (points is null)
                {
                    result.Status = PowerAcResultStatus.Incomplete;
                    result.Issues.Add(pointsIssue!);
                }
                else
                {
                    result.PointsAchieved = points;
                    result.Status = PowerAcResultStatus.Complete;
                }
            }

            return result;
        }

        public static decimal? CalculatePointsAchieved(decimal maximumPoints, decimal composite, PowerAcCalculationSettings settings, out string? issue)
        {
            issue = null;
            switch (settings.PointsMethod)
            {
                case PowerAcPointsMethods.TargetScaled:
                    if (settings.CompositeTarget <= 0m)
                    {
                        issue = "The composite target must be above zero for the TargetScaled points formula.";
                        return null;
                    }
                    return composite > settings.CompositeTarget
                        ? Math.Round(maximumPoints, 4)
                        : Math.Round(maximumPoints * composite / settings.CompositeTarget, 4);
                case PowerAcPointsMethods.Linear:
                    return Math.Round(maximumPoints * composite, 4);
                default:
                    issue = $"Unknown points formula '{settings.PointsMethod}'.";
                    return null;
            }
        }

        public static PowerAcComponentScoreResult ScoreComponent(
            PowerAcComponentConfig component,
            PowerAcMeasurement? measurement,
            bool requireVerified)
        {
            var score = new PowerAcComponentScoreResult
            {
                ComponentId = component.ComponentId,
                Code = component.Code,
                Name = component.Name,
                WeightPercent = component.WeightPercent,
                ScoringMethod = component.ScoringMethod,
                ApplicableCount = measurement?.ApplicableCount,
                CompletedCount = measurement?.CompletedCount,
                IncidentCount = measurement?.IncidentCount
            };

            if (component.UnavailableReason != null)
            {
                score.Status = PowerAcResultStatus.Pending;
                score.Message = component.UnavailableReason;
                return score;
            }

            if (component.ScoringMethod == PowerAcScoringMethods.Pending || !component.ScoringApproved)
            {
                score.Status = PowerAcResultStatus.Pending;
                score.Message = component.ScoringMethod == PowerAcScoringMethods.Pending
                    ? "Scoring formula is pending business confirmation."
                    : $"Scoring method {component.ScoringMethod} is not approved.";
                return score;
            }

            if (measurement is null)
                return Incomplete(score, "No data has been entered for this period.");
            if (!measurement.DataComplete)
                return Incomplete(score, measurement.Issues.Count > 0 ? string.Join(" ", measurement.Issues) : "Source data for the period is incomplete.");
            if (requireVerified && !measurement.Verified)
                return Incomplete(score, "Source data is not verified.");

            decimal? value;
            string? problem;
            switch (component.ScoringMethod)
            {
                case PowerAcScoringMethods.RatioCapped:
                case PowerAcScoringMethods.RatioStrict:
                case PowerAcScoringMethods.TargetScaled:
                    value = ScoreRatio(component, measurement, score, out problem);
                    break;
                case PowerAcScoringMethods.DirectPercent:
                    value = ScoreDirectPercent(measurement, score, out problem);
                    break;
                case PowerAcScoringMethods.ZeroIncidentsBinary:
                case PowerAcScoringMethods.DeductionPerIncident:
                    value = ScoreIncidents(component, measurement, out problem);
                    break;
                default:
                    value = null;
                    problem = $"Unknown scoring method '{component.ScoringMethod}'.";
                    break;
            }

            if (value is null)
                return Incomplete(score, problem ?? "The component could not be scored.");

            score.Score = Math.Round(value.Value, 6);
            score.WeightedScore = component.WeightPercent is decimal weight
                ? Math.Round(score.Score.Value * weight / 100m, 6)
                : null;
            score.Status = PowerAcResultStatus.Complete;
            score.Message = problem; // carries a warning such as capping, if any
            return score;
        }

        private static decimal? ScoreRatio(PowerAcComponentConfig component, PowerAcMeasurement m, PowerAcComponentScoreResult score, out string? problem)
        {
            problem = null;
            if (m.ApplicableCount is null || m.CompletedCount is null)
            {
                problem = "Applicable and completed counts are both required.";
                return null;
            }
            if (m.ApplicableCount < 0m || m.CompletedCount < 0m)
            {
                problem = "Counts cannot be negative.";
                return null;
            }
            if (m.ApplicableCount == 0m)
            {
                problem = "No applicable records in the period, so the ratio is undefined.";
                return null;
            }

            var ratio = m.CompletedCount.Value / m.ApplicableCount.Value;
            score.AchievementRatio = Math.Round(ratio, 6);
            var over = m.CompletedCount > m.ApplicableCount;

            switch (component.ScoringMethod)
            {
                case PowerAcScoringMethods.RatioStrict:
                    if (over)
                    {
                        problem = "Completed count exceeds applicable count, which the RatioStrict rule rejects.";
                        return null;
                    }
                    return ratio;
                case PowerAcScoringMethods.TargetScaled:
                    var target = component.TargetValue / 100m;
                    if (target <= 0m)
                    {
                        problem = "TargetScaled scoring needs a target above zero.";
                        return null;
                    }
                    return Math.Min(ratio / target, 1m);
                default: // RatioCapped
                    if (over)
                        problem = "Completed count exceeds applicable count. Score capped at 100%.";
                    return Math.Min(ratio, 1m);
            }
        }

        private static decimal? ScoreDirectPercent(PowerAcMeasurement m, PowerAcComponentScoreResult score, out string? problem)
        {
            problem = null;
            if (m.AchievementPercent is null)
            {
                problem = "Achievement percentage is required.";
                return null;
            }
            if (m.AchievementPercent < 0m || m.AchievementPercent > 100m)
            {
                problem = "Achievement percentage must be between 0 and 100.";
                return null;
            }
            score.AchievementRatio = Math.Round(m.AchievementPercent.Value / 100m, 6);
            return m.AchievementPercent.Value / 100m;
        }

        private static decimal? ScoreIncidents(PowerAcComponentConfig component, PowerAcMeasurement m, out string? problem)
        {
            problem = null;
            if (m.IncidentCount is null)
            {
                problem = "Incident count is required.";
                return null;
            }
            if (m.IncidentCount < 0)
            {
                problem = "Incident count cannot be negative.";
                return null;
            }

            if (component.ScoringMethod == PowerAcScoringMethods.ZeroIncidentsBinary)
                return m.IncidentCount == 0 ? 1m : 0m;

            if (component.ScoringParameter is null || component.ScoringParameter < 0m)
            {
                problem = "DeductionPerIncident needs a non-negative deduction percentage per incident.";
                return null;
            }
            return Math.Max(0m, 1m - m.IncidentCount.Value * component.ScoringParameter.Value / 100m);
        }

        private static PowerAcComponentScoreResult Incomplete(PowerAcComponentScoreResult score, string message)
        {
            score.Status = PowerAcResultStatus.Incomplete;
            score.Message = message;
            return score;
        }
    }
}
