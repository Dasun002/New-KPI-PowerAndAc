/*
 * File: PowerAcRules.cs
 * Shared validation rules for Power & AC configuration and data entry.
 */
using backend.Data;
using backend.Models.PowerAc;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.PowerAc
{
    public static class PowerAcRules
    {
        // Scoring methods that make sense for each component's data.
        public static string[] AllowedScoringMethods(PowerAcComponent component) => component.Code switch
        {
            PowerAcComponentCodes.CriticalSafety => new[]
            {
                PowerAcScoringMethods.Pending, PowerAcScoringMethods.ZeroIncidentsBinary, PowerAcScoringMethods.DeductionPerIncident
            },
            PowerAcComponentCodes.PreventiveMaintenance => new[]
            {
                PowerAcScoringMethods.Pending, PowerAcScoringMethods.RatioCapped, PowerAcScoringMethods.RatioStrict, PowerAcScoringMethods.TargetScaled
            },
            _ => new[]
            {
                PowerAcScoringMethods.Pending, PowerAcScoringMethods.RatioCapped, PowerAcScoringMethods.RatioStrict,
                PowerAcScoringMethods.TargetScaled, PowerAcScoringMethods.DirectPercent
            }
        };

        public static bool IsIncidentComponent(PowerAcComponent component) =>
            component.Code == PowerAcComponentCodes.CriticalSafety;

        // Fields required before a record can be submitted for verification.
        public static string? ValidateForSubmission(PowerAcComponent component, PowerAcComponentResult record)
        {
            if (IsIncidentComponent(component))
                return record.IncidentCount is null ? "Enter the incident count before submitting." : null;

            var hasCounts = record.ApplicableCount is not null && record.CompletedCount is not null;
            if (!hasCounts && record.AchievementPercent is null)
                return "Enter applicable and completed counts, or an achievement percentage, before submitting.";
            if ((record.ApplicableCount is null) != (record.CompletedCount is null))
                return "Applicable and completed counts must be entered together.";
            return null;
        }

        // True when a stored calculation snapshot exists for a period ending on or after the given month.
        // Effective-dated versions used by such snapshots are not overwritten; a new effective month must be used.
        public static async Task<bool> HasCalculatedResultsFromAsync(AppDbContext db, int year, int month, CancellationToken ct = default)
        {
            var key = PowerAcPeriod.Key(year, month);
            var periods = await db.PowerAcKpiResults.AsNoTracking()
                .Select(r => new { r.Year, r.PeriodType, r.PeriodNumber })
                .Distinct()
                .ToListAsync(ct);
            return periods.Any(p =>
            {
                var endMonth = p.PeriodType == PowerAcPeriodTypes.Quarter ? p.PeriodNumber * 3 : p.PeriodNumber;
                return PowerAcPeriod.Key(p.Year, endMonth) >= key;
            });
        }
    }
}
