/*
 * File: PowerAcKpiCalculationService.cs
 * Loads Power & AC configuration and source data for a period, runs PowerAcKpiCalculator,
 * and persists the result snapshot in a single transaction.
 */
using System.Globalization;
using System.Text.Json;
using backend.Data;
using backend.Models;
using backend.Models.PowerAc;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.PowerAc
{
    public sealed class PowerAcCalculationContext
    {
        public PowerAcPeriod Period { get; init; } = null!;
        public List<PowerAcComponent> Components { get; init; } = new();
        public List<PowerAcComponentConfig> ComponentConfigs { get; init; } = new();
        public List<PowerAcEngineerGroup> Groups { get; init; } = new();
        public PowerAcCalculationSetting? Setting { get; init; }
        public PowerAcCalculationSettings Settings { get; init; } = new();
        public PowerAcCalculationOutput Output { get; init; } = new();
        // Inputs per group (data presence, verification), used by the readiness report.
        public List<PowerAcGroupInput> GroupInputs { get; init; } = new();
        // Scoring version effective for the period, per component id.
        public Dictionary<int, PowerAcComponentVersion?> Versions { get; init; } = new();
    }

    public class PowerAcKpiCalculationService
    {
        private readonly AppDbContext _db;

        public PowerAcKpiCalculationService(AppDbContext db)
        {
            _db = db;
        }

        // Calculates without saving anything.
        public async Task<PowerAcCalculationContext> PreviewAsync(PowerAcPeriod period, CancellationToken ct = default)
        {
            var components = await _db.PowerAcComponents.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .ToListAsync(ct);

            var endKey = period.EndKey;
            var weightRows = await _db.PowerAcComponentWeights.AsNoTracking().ToListAsync(ct);
            var versionRows = await _db.PowerAcComponentVersions.AsNoTracking().ToListAsync(ct);
            var versions = new Dictionary<int, PowerAcComponentVersion?>();
            var configs = components.Select(c =>
            {
                var weight = weightRows
                    .Where(w => w.ComponentId == c.Id && PowerAcPeriod.Key(w.EffectiveYear, w.EffectiveMonth) <= endKey)
                    .OrderByDescending(w => PowerAcPeriod.Key(w.EffectiveYear, w.EffectiveMonth))
                    .FirstOrDefault();
                // Scoring method, target and approval come from the version effective for the period.
                var version = versionRows
                    .Where(v => v.ComponentId == c.Id && PowerAcPeriod.Key(v.EffectiveYear, v.EffectiveMonth) <= endKey)
                    .OrderByDescending(v => PowerAcPeriod.Key(v.EffectiveYear, v.EffectiveMonth))
                    .FirstOrDefault();
                versions[c.Id] = version;
                return version is null
                    ? new PowerAcComponentConfig(c.Id, c.Code, c.Name, c.TargetValue, PowerAcScoringMethods.Pending,
                        null, false, weight?.WeightPercent, null, $"No scoring version is effective for {period}.")
                    : new PowerAcComponentConfig(c.Id, c.Code, c.Name, version.TargetValue, version.ScoringMethod,
                        version.ScoringParameter, version.ScoringApproved, weight?.WeightPercent, version.Id);
            }).ToList();

            var setting = (await _db.PowerAcCalculationSettings.AsNoTracking().ToListAsync(ct))
                .Where(s => PowerAcPeriod.Key(s.EffectiveYear, s.EffectiveMonth) <= endKey)
                .OrderByDescending(s => PowerAcPeriod.Key(s.EffectiveYear, s.EffectiveMonth))
                .FirstOrDefault();

            var settingIssues = new List<string>();
            decimal? pointsApplicable = null;
            if (setting is null)
            {
                settingIssues.Add($"No Power & AC calculation settings are effective for {period}.");
            }
            else
            {
                var kpi = await _db.KpiDefinitions.AsNoTracking().FirstOrDefaultAsync(k => k.Id == setting.KpiDefinitionId, ct);
                if (kpi is null)
                    settingIssues.Add($"KPI definition {setting.KpiDefinitionId} was not found, so Points Applicable is unknown.");
                else
                    pointsApplicable = kpi.PointsApplicable;
            }

            var settings = new PowerAcCalculationSettings
            {
                PointsApplicable = pointsApplicable,
                PointsMethod = setting?.PointsMethod ?? PowerAcPointsMethods.TargetScaled,
                CompositeTarget = setting?.CompositeTarget ?? 1m,
                PointsMethodApproved = setting?.PointsMethodApproved ?? false,
                RequireVerifiedData = setting?.RequireVerifiedData ?? true,
                Issues = settingIssues
            };

            var groups = await _db.PowerAcEngineerGroups.AsNoTracking()
                .Where(g => g.IsActive)
                .OrderBy(g => g.SortOrder).ThenBy(g => g.Id)
                .ToListAsync(ct);

            var groupInputs = await BuildGroupInputsAsync(period, components, groups, ct);
            var output = PowerAcKpiCalculator.Calculate(configs, groupInputs, settings);

            return new PowerAcCalculationContext
            {
                Period = period,
                Components = components,
                ComponentConfigs = configs,
                Groups = groups,
                Setting = setting,
                Settings = settings,
                Output = output,
                GroupInputs = groupInputs,
                Versions = versions
            };
        }

        // Calculates and replaces the stored snapshot for the period in one transaction.
        public async Task<PowerAcCalculationContext> CalculateAndPersistAsync(PowerAcPeriod period, string? calculatedBy, CancellationToken ct = default)
        {
            var context = await PreviewAsync(period, ct);
            var now = DateTime.UtcNow;

            var useTransaction = _db.Database.IsRelational();
            await using var transaction = useTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;

            var existing = await _db.PowerAcKpiResults
                .Include(r => r.ComponentScores)
                .Where(r => r.Year == period.Year && r.PeriodType == period.PeriodType && r.PeriodNumber == period.PeriodNumber)
                .ToListAsync(ct);
            if (existing.Count > 0)
            {
                _db.PowerAcKpiResults.RemoveRange(existing);
                await _db.SaveChangesAsync(ct);
            }

            foreach (var group in context.Output.Groups)
            {
                var issues = context.Output.GlobalIssues.Concat(group.Issues).Distinct().ToList();
                _db.PowerAcKpiResults.Add(new PowerAcKpiResult
                {
                    EngineerGroupId = group.EngineerGroupId,
                    Year = period.Year,
                    PeriodType = period.PeriodType,
                    PeriodNumber = period.PeriodNumber,
                    Status = group.Status,
                    CompositeAchievement = group.CompositeAchievement,
                    PointsApplicable = group.PointsApplicable,
                    AllocationValue = group.AllocationValue,
                    TotalAllocation = group.TotalAllocation,
                    MaximumPoints = group.MaximumPoints,
                    PointsAchieved = group.PointsAchieved,
                    PointsMethod = context.Settings.PointsMethod,
                    CompositeTarget = context.Settings.CompositeTarget,
                    CalculationSettingId = context.Setting?.Id,
                    IssuesJson = JsonSerializer.Serialize(new { issues, warnings = group.Warnings }),
                    CalculatedAt = now,
                    CalculatedBy = calculatedBy,
                    ComponentScores = group.Components.Select(c => new PowerAcKpiComponentScore
                    {
                        ComponentId = c.ComponentId,
                        WeightPercent = c.WeightPercent,
                        ComponentVersionId = context.ComponentConfigs.FirstOrDefault(x => x.ComponentId == c.ComponentId)?.VersionId,
                        ScoringMethod = c.ScoringMethod,
                        ApplicableCount = c.ApplicableCount,
                        CompletedCount = c.CompletedCount,
                        IncidentCount = c.IncidentCount,
                        AchievementRatio = c.AchievementRatio,
                        Score = c.Score,
                        WeightedScore = c.WeightedScore,
                        Status = c.Status,
                        Message = Truncate(c.Message, 500)
                    }).ToList()
                });
            }

            await _db.SaveChangesAsync(ct);

            PowerAcAudit.Add(_db, PowerAcAuditEntities.KpiCalculation, PowerAcPeriod.Key(period.Year, period.EndMonth),
                PowerAcAuditActions.Calculated, null, null, calculatedBy, new
                {
                    period = period.ToString(),
                    replacedRows = existing.Count,
                    complete = context.Output.Groups.Count(g => g.Status == PowerAcResultStatus.Complete),
                    incomplete = context.Output.Groups.Count(g => g.Status == PowerAcResultStatus.Incomplete),
                    pending = context.Output.Groups.Count(g => g.Status == PowerAcResultStatus.Pending),
                    settingId = context.Setting?.Id,
                    versionIds = context.ComponentConfigs.Select(c => c.VersionId)
                });
            await _db.SaveChangesAsync(ct);

            if (transaction != null)
                await transaction.CommitAsync(ct);

            return context;
        }

        private async Task<List<PowerAcGroupInput>> BuildGroupInputsAsync(
            PowerAcPeriod period,
            List<PowerAcComponent> components,
            List<PowerAcEngineerGroup> groups,
            CancellationToken ct)
        {
            var groupIds = groups.Select(g => g.Id).ToList();
            var endKey = period.EndKey;

            var allocations = (await _db.PowerAcAreaAllocations.AsNoTracking()
                    .Where(a => groupIds.Contains(a.EngineerGroupId))
                    .ToListAsync(ct))
                .Where(a => PowerAcPeriod.Key(a.EffectiveYear, a.EffectiveMonth) <= endKey)
                .GroupBy(a => a.EngineerGroupId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => PowerAcPeriod.Key(a.EffectiveYear, a.EffectiveMonth)).First());

            var pmMonths = period.PmMonths.Select(m => (int)m).ToList();
            var designations = groups.Where(g => !string.IsNullOrWhiteSpace(g.PmDesignation)).Select(g => g.PmDesignation!).ToList();
            var pmRows = await _db.PowerAndAC.AsNoTracking()
                .Where(p => p.Year == period.Year && pmMonths.Contains(p.Month) && designations.Contains(p.Designation))
                .ToListAsync(ct);

            var sourceMonths = period.SourceMonths.ToList();
            var componentRows = await _db.PowerAcComponentResults.AsNoTracking()
                .Where(r => r.Year == period.Year && sourceMonths.Contains(r.Month) && groupIds.Contains(r.EngineerGroupId))
                .ToListAsync(ct);

            var inputs = new List<PowerAcGroupInput>();
            foreach (var group in groups)
            {
                var measurements = new Dictionary<int, PowerAcMeasurement>();
                foreach (var component in components)
                {
                    var measurement = component.DataSource == PowerAcDataSources.PowerAndAC
                        ? BuildPmMeasurement(period, group, pmRows)
                        : BuildComponentMeasurement(period, group, component, componentRows);
                    if (measurement != null)
                        measurements[component.Id] = measurement;
                }

                inputs.Add(new PowerAcGroupInput
                {
                    EngineerGroupId = group.Id,
                    Code = group.Code,
                    MappingVerified = group.MappingVerified && group.RegionDataId.HasValue,
                    AllocationValue = allocations.TryGetValue(group.Id, out var allocation) ? allocation.AllocationValue : null,
                    Measurements = measurements
                });
            }
            return inputs;
        }

        // Preventive Maintenance: quarter totals from monthly Scheduled/Attended. Cumulative snapshots are never summed.
        public static PowerAcMeasurement? BuildPmMeasurement(PowerAcPeriod period, PowerAcEngineerGroup group, IReadOnlyList<PowerAndAC> pmRows)
        {
            if (string.IsNullOrWhiteSpace(group.PmDesignation))
            {
                return new PowerAcMeasurement
                {
                    DataComplete = false,
                    Issues = { "No PowerAndAC designation is mapped to this engineer group." }
                };
            }

            var rows = pmRows.Where(p => p.Designation == group.PmDesignation && p.Year == period.Year).ToList();
            var issues = new List<string>();
            var warnings = new List<string>();
            var used = new List<PowerAndAC>();

            foreach (var month in period.PmMonths)
            {
                var monthRows = rows.Where(r => r.Month == month).ToList();
                if (monthRows.Count == 0)
                {
                    issues.Add($"Preventive Maintenance data is missing for {MonthName(month)} {period.Year}.");
                    continue;
                }
                if (monthRows.Count > 1)
                {
                    issues.Add($"Duplicate Preventive Maintenance rows exist for {MonthName(month)} {period.Year}.");
                    continue;
                }
                var row = monthRows[0];
                if (row.Scheduled < 0 || row.Attended < 0)
                    issues.Add($"Negative Preventive Maintenance counts exist for {MonthName(month)} {period.Year}.");
                if (row.Attended > row.Scheduled)
                    warnings.Add($"Attended ({row.Attended}) exceeds Scheduled ({row.Scheduled}) in {MonthName(month)} {period.Year}.");
                used.Add(row);
            }

            if (issues.Count > 0)
                return new PowerAcMeasurement { DataComplete = false, Issues = issues, Warnings = warnings };

            var scheduled = used.Sum(r => (decimal)r.Scheduled);
            var attended = used.Sum(r => (decimal)r.Attended);

            // Cross-check against the trigger-maintained quarter-to-date snapshot of the last month (informational only).
            var last = used.OrderBy(r => r.Month).Last();
            if (last.Cumulative_Sched != (int)scheduled || last.Cumulative_Achieved != (int)attended)
                warnings.Add($"Quarter-to-date totals ({scheduled}/{attended}) differ from the stored cumulative snapshot ({last.Cumulative_Sched}/{last.Cumulative_Achieved}) for {MonthName(last.Month)}.");

            return new PowerAcMeasurement
            {
                ApplicableCount = scheduled,
                CompletedCount = attended,
                DataComplete = true,
                Verified = used.All(r => r.IsVerified),
                Warnings = warnings
            };
        }

        // Safety, Corrective Action and Availability: one record per month; a quarter needs all three months.
        public static PowerAcMeasurement? BuildComponentMeasurement(
            PowerAcPeriod period,
            PowerAcEngineerGroup group,
            PowerAcComponent component,
            IReadOnlyList<PowerAcComponentResult> rows)
        {
            var relevant = rows.Where(r => r.EngineerGroupId == group.Id && r.ComponentId == component.Id && r.Year == period.Year).ToList();
            var issues = new List<string>();
            var used = new List<PowerAcComponentResult>();
            foreach (var month in period.SourceMonths)
            {
                var row = relevant.FirstOrDefault(r => r.Month == month);
                if (row is null)
                    issues.Add($"{component.Name} data is missing for {MonthName(month)} {period.Year}.");
                else
                    used.Add(row);
            }

            if (used.Count == 0 && period.PeriodType == PowerAcPeriodTypes.Month)
                return null;
            if (issues.Count > 0)
                return new PowerAcMeasurement { DataComplete = false, Issues = issues };

            return new PowerAcMeasurement
            {
                ApplicableCount = SumOrNull(used.Select(r => r.ApplicableCount)),
                CompletedCount = SumOrNull(used.Select(r => r.CompletedCount)),
                IncidentCount = used.Any(r => r.IncidentCount is null) ? null : used.Sum(r => r.IncidentCount!.Value),
                AchievementPercent = used.Any(r => r.AchievementPercent is null) ? null : Math.Round(used.Average(r => r.AchievementPercent!.Value), 4),
                DataComplete = true,
                Verified = used.All(r => r.Status == PowerAcRecordStatus.Verified)
            };
        }

        private static decimal? SumOrNull(IEnumerable<decimal?> values)
        {
            var list = values.ToList();
            return list.Any(v => v is null) ? null : list.Sum(v => v!.Value);
        }

        private static string MonthName(int month) => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);

        private static string? Truncate(string? value, int max) =>
            value is null || value.Length <= max ? value : value[..max];
    }
}
