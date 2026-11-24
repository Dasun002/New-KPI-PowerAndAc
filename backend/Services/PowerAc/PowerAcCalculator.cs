/*
 * File: PowerAcCalculator.cs
 * Power & A/C KPI rules and calculation. The rules are in code, as they are for the other KPIs; the data is in
 * dbo.PowerAcPmData.
 *
 *   component score   = Achievement % / 100 (each component's Achievement % is entered directly)
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
                    if (result.Achievement[c.Code] is null) result.Issues.Add($"{c.Name} Achievement % is missing.");
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
