/*
 * File: PowerAcScoring.cs
 * Converts the typed inputs of two Power & A/C KPIs into the achievement % that the composite uses.
 * The result is stored with the row when it is saved, so later changes to these settings do not
 * recalculate saved months.
 *
 *   Critical Safety          achievement % = max(0, 100 - incidents x deduction per incident)
 *                            (deduction is configured as PowerAc:SafetyDeductionPerIncident, default 10)
 *   Power System Availability achievement % = min(100, actual availability % / target % x 100)
 *                            (target is configured as PowerAc:AvailabilityTargetPercent, default 99)
 */
namespace backend.Services.PowerAc
{
    public static class PowerAcScoring
    {
        public const string SafetyCode = "SAFETY";
        public const string AvailabilityCode = "AVAILABILITY";

        public const decimal DefaultSafetyDeductionPerIncident = 10m;
        public const decimal DefaultAvailabilityTargetPercent = 99m;

        // Upper limit for a typed incident count. Anything above it is almost certainly a typing error.
        public const int MaxIncidentCount = 1000;

        public sealed record Settings(decimal SafetyDeductionPerIncident, decimal AvailabilityTargetPercent);

        // Reads and validates the scoring settings. Throws InvalidOperationException for an invalid value, so an
        // invalid configuration is reported instead of producing a score.
        public static Settings LoadSettings(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            var deduction = ReadDecimal(configuration, "PowerAc:SafetyDeductionPerIncident", DefaultSafetyDeductionPerIncident);
            var target = ReadDecimal(configuration, "PowerAc:AvailabilityTargetPercent", DefaultAvailabilityTargetPercent);
            if (deduction < 0m || deduction > 100m)
                throw new InvalidOperationException("PowerAc:SafetyDeductionPerIncident must be between 0 and 100.");
            if (target <= 0m || target > 100m)
                throw new InvalidOperationException("PowerAc:AvailabilityTargetPercent must be greater than 0 and at most 100.");
            return new Settings(deduction, target);
        }

        private static decimal ReadDecimal(Microsoft.Extensions.Configuration.IConfiguration configuration, string key, decimal fallback)
        {
            var text = configuration[key];
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException($"{key} must be a number.");
            return value;
        }

        public static decimal SafetyAchievement(int incidentCount, decimal deductionPerIncident)
        {
            if (incidentCount < 0) throw new ArgumentOutOfRangeException(nameof(incidentCount), "Incident count cannot be negative.");
            return Math.Round(Math.Max(0m, 100m - incidentCount * deductionPerIncident), 4);
        }

        public static decimal AvailabilityAchievement(decimal actualPercent, decimal targetPercent)
        {
            if (targetPercent <= 0m || targetPercent > 100m)
                throw new ArgumentOutOfRangeException(nameof(targetPercent), "Availability target must be greater than 0 and at most 100.");
            return Math.Round(Math.Min(100m, actualPercent / targetPercent * 100m), 4);
        }
    }
}
