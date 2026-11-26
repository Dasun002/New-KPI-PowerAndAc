/*
 * File: PowerAcCalculator.cs
 * Power & A/C KPI rules and calculation. The rules are in code, as they are for the other KPIs; the data is in
 * dbo.PowerAcPmData.
 *
 *   component score   = Achievement % / 100. Preventive Maintenance, Corrective Action are entered as Achievement %.
 *                       Critical Safety and Availability are scored from their inputs on save (PowerAcScoring).
 *   composite         = sum(score x weight / 100); weights are 25% each
 *   maximum points    = Points Applicable (KPI 2017) x group allocation / total of all allocations
 *   points achieved   = maximum if composite > target (1), otherwise maximum x composite / target
 *
 * A group is Complete only when all four Achievement % values, its allocation, and the row's verification are present.
 * Missing data is never treated as zero.
 */
using backend.Models.PowerAc;

namespace backend.Services.PowerAc
{
    public static class PowerAcConfig
    {
        public const int PointsKpiDefinitionId = 2017;
        public const decimal CompositeTarget = 1m;

        // A Power & A/C KPI row from dbo.KpiDefinition (Category = "PowerAndAC"). Code is stored in StrategicObjectives.
        public sealed record Component(int Id, string Code, string Name, decimal WeightPercent, string Target, decimal? TargetValue);

        public static Component ToComponent(backend.Models.KpiDefinition k)
        {
            var target = $"{k.DescriptionOfKPI?.Trim()} {k.Unit?.Trim()}".Trim();
            var value = decimal.TryParse(k.DescriptionOfKPI?.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null;
            return new Component(k.Id, (k.StrategicObjectives ?? string.Empty).Trim().ToUpperInvariant(), k.KeyPerformanceIndicators, k.Weightage, target, value);
        }

        public sealed record Group(string Code, string Label, string Designation);

        // The 20 engineer groups, matching dbo.PowerAcPmData.designation.
        public static readonly Group[] Groups =
        {
            new("EP", "E/NW/ EPS", "NW/EP"), new("CPN", "E/NW/CPN", "NW/CPN"), new("CPS", "E/NW/CPS", "NW/CPS"),
            new("NCP", "E/NW/NCP", "NW/NCP"), new("NP-2", "E/NW/NP2", "NW/NP-2"), new("NP-1", "E/NW/NP1", "NW/NP-1"),
            new("NWPE", "E/NW/NWPE", "NW/NWPE"), new("NWPW", "E/NW/NWPW", "NW/NWPW"), new("SAB", "E/NW/SAB", "NW/SAB"),
            new("SPE", "E/NW/SPE", "NW/SPE"), new("SPW", "E/NW/SPW", "NW/SPW"), new("UVA", "E/NW/UVA", "NW/UVA"),
            new("WPC-1", "E/NW/WPC1", "NW/WPC-1"), new("WPC-2", "E/NW/WPC2", "NW/WPC-2"), new("WPE", "E/NW/WPE", "NW/WPE"),
            new("WPN", "E/NW/WPN", "NW/WPN"), new("WPNE", "E/NW/WPNE", "NW/WPNE"), new("WPS", "E/NW/WPS", "NW/WPS"),
            new("WPSE", "E/NW/WPSE", "NW/WPSE"), new("WPSW", "E/NW/WPSW", "NW/WPSW"),
        };

        public const string Category = "PowerAndAC";

        public static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December",
        };
    }

    public sealed class PowerAcGroupResult
    {
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Designation { get; init; } = string.Empty;
        public Dictionary<string, decimal?> Achievement { get; } = new();
        public decimal? AllocationValue { get; set; }
        public bool IsVerified { get; set; }
        public decimal? Composite { get; set; }
        public decimal? MaximumPoints { get; set; }
        public decimal? PointsAchieved { get; set; }
        public string Status { get; set; } = "Incomplete";
        public List<string> Issues { get; } = new();
        // Rule-scored KPIs (SAFETY, AVAILABILITY) whose score comes from an old percentage-only entry.
        // These are still scored as saved. They are reported so the user can re-enter the input. Not an issue.
        public List<string> LegacyCodes { get; } = new();
    }

    public static class PowerAcCalculator
    {
        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new();

        // Achievement % of one KPI. The four original KPIs have their own columns; any other KPI is read from achievements_json.
        public static decimal? AchievementOf(PowerAcPmData row, string code) => code switch
        {
            "PM" => row.PmAchievement,
            "SAFETY" => row.SafetyAchievement,
            "CORRECTIVE" => row.CorrectiveAchievement,
            "AVAILABILITY" => row.AvailabilityAchievement,
            _ => ReadJson(row.AchievementsJson).TryGetValue(code, out var v) ? v : null
        };

        public const string MonthlyDistributionRequirement = "Monthly distribution (Scheduled or Attended)";

        // The requirements for verifying a group. Used by both the bulk and the single-group verification, so they always match.
        // Zero is a valid value; only null is missing. A legacy percentage-only Safety or Availability entry has no typed input,
        // so it counts as missing. The monthly distribution is satisfied by a Scheduled or an Attended value (0 included).
        public static List<string> MissingVerificationInputs(PowerAcPmData row)
        {
            var missing = new List<string>();
            if (row.PmAchievement is null) missing.Add("PM Achievement");
            if (row.SafetyIncidentCount is null) missing.Add("Critical Safety Incident Count");
            if (row.CorrectiveAchievement is null) missing.Add("Corrective Action Achievement");
            if (row.AvailabilityActualPct is null) missing.Add("Actual Power System Availability");
            if (row.AreaAllocation is null) missing.Add("Area Allocation");
            if (row.Scheduled is null && row.Attended is null) missing.Add(MonthlyDistributionRequirement);
            return missing;
        }

        public static string VerificationMessage(string designation, IEnumerable<string> missing) =>
            $"Cannot verify {designation}. Missing: {string.Join(", ", missing)}.";

        // Verified = passes the requirements and is verified. VerifiedNeedsReentry = verified, but no longer passes
        // (for example a legacy percentage, or a value that was never entered). Ready = passes, not yet verified. Incomplete = missing values.
        public const string StatusVerified = "Verified";
        public const string StatusVerifiedNeedsReentry = "VerifiedNeedsReentry";
        public const string StatusReady = "ReadyToVerify";
        public const string StatusIncomplete = "Incomplete";

        public static string VerificationStatusOf(PowerAcPmData row)
        {
            var passes = MissingVerificationInputs(row).Count == 0;
            if (row.IsVerified) return passes && LegacyCodesOf(row).Count == 0 ? StatusVerified : StatusVerifiedNeedsReentry;
            return passes ? StatusReady : StatusIncomplete;
        }

        public static bool HasAnyVerificationInput(PowerAcPmData row) =>
            row.PmAchievement is not null || row.SafetyIncidentCount is not null || row.CorrectiveAchievement is not null ||
            row.AvailabilityActualPct is not null || row.AreaAllocation is not null ||
            row.Scheduled is not null || row.Attended is not null;

        // Rule-scored KPIs that hold an old percentage-only entry: the score exists but the typed input does not.
        public static List<string> LegacyCodesOf(PowerAcPmData row)
        {
            var codes = new List<string>();
            if (row.SafetyIncidentCount is null && row.SafetyAchievement is not null) codes.Add(PowerAcScoring.SafetyCode);
            if (row.AvailabilityActualPct is null && row.AvailabilityAchievement is not null) codes.Add(PowerAcScoring.AvailabilityCode);
            return codes;
        }

        public static void SetAchievement(PowerAcPmData row, string code, decimal? value)
        {
            switch (code)
            {
                case "PM": row.PmAchievement = value; return;
                case "SAFETY": row.SafetyAchievement = value; return;
                case "CORRECTIVE": row.CorrectiveAchievement = value; return;
                case "AVAILABILITY": row.AvailabilityAchievement = value; return;
            }
            var dict = ReadJson(row.AchievementsJson);
            if (value is null) dict.Remove(code); else dict[code] = value;
            row.AchievementsJson = dict.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(dict, JsonOptions);
        }

        private static Dictionary<string, decimal?> ReadJson(string? json) =>
            string.IsNullOrWhiteSpace(json)
                ? new Dictionary<string, decimal?>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal?>>(json) ?? new Dictionary<string, decimal?>();

        public static List<PowerAcGroupResult> Calculate(IReadOnlyList<PowerAcPmData> rows, IReadOnlyList<PowerAcConfig.Component> components, decimal? pointsApplicable)
        {
            var results = new List<PowerAcGroupResult>();
            foreach (var group in PowerAcConfig.Groups)
            {
                var row = rows.FirstOrDefault(r => string.Equals(r.Designation.Trim(), group.Designation, StringComparison.OrdinalIgnoreCase));
                var result = new PowerAcGroupResult { Code = group.Code, Label = group.Label, Designation = group.Designation };
                foreach (var c in components)
                    result.Achievement[c.Code] = row is null ? null : AchievementOf(row, c.Code);
                result.AllocationValue = row?.AreaAllocation;
                result.IsVerified = row?.IsVerified ?? false;

                if (row is null) result.Issues.Add("No data row for this engineer group and month.");
                if (components.Count == 0) result.Issues.Add("No Power & A/C KPIs are configured.");
                foreach (var c in components)
                {
                    if (result.Achievement[c.Code] is not null) continue;
                    result.Issues.Add(c.Code switch
                    {
                        PowerAcScoring.SafetyCode => "Critical Safety incident count is missing.",
                        PowerAcScoring.AvailabilityCode => "Power System Availability actual % is missing.",
                        _ => $"{c.Name} Achievement % is missing.",
                    });
                }
                if (row is not null)
                    result.LegacyCodes.AddRange(LegacyCodesOf(row).Where(code => components.Any(c => c.Code == code)));
                if (result.AllocationValue is null) result.Issues.Add("Area allocation is missing.");
                if (row is not null && !row.IsVerified) result.Issues.Add("Data is not verified.");
                results.Add(result);
            }

            // The weights of all configured KPIs must total exactly 100%, otherwise no group can be scored.
            var weightTotal = components.Sum(c => c.WeightPercent);
            if (components.Count > 0 && Math.Abs(weightTotal - 100m) > 0.0001m)
                foreach (var r in results) r.Issues.Add($"KPI weights total {weightTotal:0.####}%. They must total 100%.");

            // Composite per group, when all four values are present.
            foreach (var r in results)
            {
                if (components.Count == 0 || r.Achievement.Values.Any(v => v is null)) continue;
                r.Composite = Math.Round(components.Sum(c => (r.Achievement[c.Code]!.Value / 100m) * (c.WeightPercent / 100m)), 6);
            }

            // Maximum points need all 20 allocations and a positive total.
            var allAllocated = results.All(r => r.AllocationValue is not null);
            var total = allAllocated ? results.Sum(r => r.AllocationValue!.Value) : 0m;
            if (!allAllocated)
                foreach (var r in results) r.Issues.Add("Points are not calculated until all 20 area allocations are entered.");
            else if (total <= 0m)
                foreach (var r in results) r.Issues.Add("Total area allocation is zero.");

            foreach (var r in results)
            {
                if (pointsApplicable is null) r.Issues.Add($"Points Applicable is not set for KPI {PowerAcConfig.PointsKpiDefinitionId}.");
                if (allAllocated && total > 0m && pointsApplicable is decimal pa && r.AllocationValue is decimal alloc)
                    r.MaximumPoints = Math.Round(pa * alloc / total, 4);

                if (r.Issues.Count == 0 && r.Composite is decimal composite && r.MaximumPoints is decimal max)
                {
                    r.PointsAchieved = composite > PowerAcConfig.CompositeTarget
                        ? Math.Round(max, 4)
                        : Math.Round(max * composite / PowerAcConfig.CompositeTarget, 4);
                    r.Status = "Complete";
                }
            }
            return results;
        }
    }
}
