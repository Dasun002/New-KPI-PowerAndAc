/*
 * File: PowerAcKpiController.cs
 * Power & AC KPI results: live preview, calculate-and-save (authorized users only, never anonymous)
 * and stored monthly/quarterly snapshots with a per-component breakdown.
 */
using System.Text.Json;
using backend.Data;
using backend.DTOs.PowerAc;
using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/power-ac/kpi")]
    [Authorize]
    public class PowerAcKpiController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PowerAcAccessService _access;
        private readonly PowerAcKpiCalculationService _calculation;

        public PowerAcKpiController(AppDbContext db, PowerAcAccessService access, PowerAcKpiCalculationService calculation)
        {
            _db = db;
            _access = access;
            _calculation = calculation;
        }

        // Calculates from current data without saving.
        [HttpGet("preview")]
        public async Task<ActionResult<PowerAcKpiResultsDto>> Preview([FromQuery] int year, [FromQuery] string? periodType, [FromQuery] int period)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            if (!PowerAcPeriod.TryCreate(year, periodType, period, out var p, out var error))
                return BadRequest(new { message = error });

            var context = await _calculation.PreviewAsync(p!);
            return Ok(await ToDtoAsync(context, isPreview: true, calculatedAt: null, calculatedBy: null));
        }

        // Calculates and replaces the stored snapshot for the period.
        [HttpPost("calculate")]
        public async Task<ActionResult<PowerAcKpiResultsDto>> Calculate([FromQuery] int year, [FromQuery] string? periodType, [FromQuery] int period)
        {
            if (!await _access.CanCalculateAsync(User)) return Forbid();
            if (!PowerAcPeriod.TryCreate(year, periodType, period, out var p, out var error))
                return BadRequest(new { message = error });

            var user = PowerAcAccessService.CurrentUserName(User);
            var context = await _calculation.CalculateAndPersistAsync(p!, user);
            return Ok(await ToDtoAsync(context, isPreview: false, calculatedAt: DateTime.UtcNow, calculatedBy: user));
        }

        // Readiness of every engineer group for the period: configured, data complete, verified, approved, score-ready.
        [HttpGet("readiness")]
        public async Task<ActionResult<PowerAcReadinessReport>> Readiness([FromQuery] int year, [FromQuery] string? periodType, [FromQuery] int period)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            if (!PowerAcPeriod.TryCreate(year, periodType, period, out var p, out var error))
                return BadRequest(new { message = error });

            var context = await _calculation.PreviewAsync(p!);
            return Ok(PowerAcReadiness.Build(context));
        }

        // Returns the stored snapshot for the period. Empty groups mean it has not been calculated yet.
        [HttpGet("results")]
        public async Task<ActionResult<PowerAcKpiResultsDto>> GetResults([FromQuery] int year, [FromQuery] string? periodType, [FromQuery] int period)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            if (!PowerAcPeriod.TryCreate(year, periodType, period, out var p, out var error))
                return BadRequest(new { message = error });

            var rows = await _db.PowerAcKpiResults.AsNoTracking()
                .Include(r => r.ComponentScores)
                .Include(r => r.EngineerGroup).ThenInclude(g => g!.RegionData)
                .Where(r => r.Year == p!.Year && r.PeriodType == p.PeriodType && r.PeriodNumber == p.PeriodNumber)
                .OrderBy(r => r.EngineerGroup!.SortOrder).ThenBy(r => r.EngineerGroupId)
                .ToListAsync();

            var components = await _db.PowerAcComponents.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync();
            var first = rows.FirstOrDefault();

            var groups = rows.Select(r =>
            {
                var notes = ParseIssues(r.IssuesJson);
                return new PowerAcGroupResultDto
                {
                    EngineerGroupId = r.EngineerGroupId,
                    EngineerGroupCode = r.EngineerGroup?.Code ?? string.Empty,
                    WorkbookLabel = r.EngineerGroup?.WorkbookLabel ?? string.Empty,
                    NetworkEngineer = r.EngineerGroup?.RegionData?.NetworkEngineer,
                    LeaCode = r.EngineerGroup?.RegionData?.LeaCode,
                    Status = r.Status,
                    CompositeAchievement = r.CompositeAchievement,
                    PointsApplicable = r.PointsApplicable,
                    AllocationValue = r.AllocationValue,
                    TotalAllocation = r.TotalAllocation,
                    MaximumPoints = r.MaximumPoints,
                    PointsAchieved = r.PointsAchieved,
                    Issues = notes.Issues,
                    Warnings = notes.Warnings,
                    Components = r.ComponentScores
                        .OrderBy(s => components.FirstOrDefault(c => c.Id == s.ComponentId)?.SortOrder ?? 0)
                        .Select(s =>
                        {
                            var c = components.FirstOrDefault(x => x.Id == s.ComponentId);
                            return new PowerAcComponentScoreDto
                            {
                                ComponentId = s.ComponentId,
                                Code = c?.Code ?? string.Empty,
                                Name = c?.Name ?? string.Empty,
                                WeightPercent = s.WeightPercent,
                                ComponentVersionId = s.ComponentVersionId,
                                ScoringMethod = s.ScoringMethod,
                                ApplicableCount = s.ApplicableCount,
                                CompletedCount = s.CompletedCount,
                                IncidentCount = s.IncidentCount,
                                AchievementRatio = s.AchievementRatio,
                                Score = s.Score,
                                WeightedScore = s.WeightedScore,
                                Status = s.Status,
                                Message = s.Message
                            };
                        }).ToList()
                };
            }).ToList();

            // Weights come from the snapshot itself (what was actually used), not from today's configuration.
            var snapshotWeights = rows
                .SelectMany(r => r.ComponentScores)
                .GroupBy(s => s.ComponentId)
                .ToDictionary(g => g.Key, g => g.Select(s => s.WeightPercent).FirstOrDefault(w => w != null));
            var usedComponentIds = snapshotWeights.Keys.ToHashSet();
            var weightTotal = snapshotWeights.Values.Sum(w => w ?? 0m);
            var weightsValid = rows.Count > 0
                && snapshotWeights.Values.All(w => w != null)
                && Math.Abs(weightTotal - 100m) <= 0.0001m;

            // Scoring versions recorded in the snapshot: show the method and approval that were actually used.
            var versionIds = rows.SelectMany(r => r.ComponentScores).Where(sc => sc.ComponentVersionId != null)
                .Select(sc => sc.ComponentVersionId!.Value).Distinct().ToList();
            var snapshotVersions = (await _db.PowerAcComponentVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id)).ToListAsync())
                .GroupBy(v => v.ComponentId).ToDictionary(g => g.Key, g => (PowerAcComponentVersion?)g.First());

            // Approval and verification policy that applied to the period (not stored on each result row).
            var setting = (await _db.PowerAcCalculationSettings.AsNoTracking().ToListAsync())
                .Where(s => PowerAcPeriod.Key(s.EffectiveYear, s.EffectiveMonth) <= p!.EndKey)
                .OrderByDescending(s => PowerAcPeriod.Key(s.EffectiveYear, s.EffectiveMonth))
                .FirstOrDefault();

            // Issues shared by every group are period-wide (for example missing allocations); show them once.
            var globalIssues = groups.Count == 0
                ? new List<string>()
                : groups.Skip(1).Aggregate(
                    (IEnumerable<string>)groups[0].Issues,
                    (acc, g) => acc.Intersect(g.Issues)).ToList();
            foreach (var g in groups)
                g.Issues = g.Issues.Except(globalIssues).ToList();

            var dto = new PowerAcKpiResultsDto
            {
                Year = p!.Year,
                PeriodType = p.PeriodType,
                PeriodNumber = p.PeriodNumber,
                IsPreview = false,
                CalculatedAt = first?.CalculatedAt,
                CalculatedBy = first?.CalculatedBy,
                PointsMethod = first?.PointsMethod ?? string.Empty,
                CompositeTarget = first?.CompositeTarget ?? 0m,
                PointsMethodApproved = setting?.PointsMethodApproved ?? false,
                RequireVerifiedData = setting?.RequireVerifiedData ?? true,
                PointsApplicable = first?.PointsApplicable,
                TotalAllocation = first?.TotalAllocation,
                AllocationsComplete = first?.TotalAllocation != null,
                WeightsValid = weightsValid,
                WeightTotal = weightTotal,
                GlobalIssues = globalIssues,
                Components = components
                    .Where(c => usedComponentIds.Count == 0 || usedComponentIds.Contains(c.Id))
                    .Select(c => PowerAcConfigController.ToDto(c,
                        snapshotWeights.TryGetValue(c.Id, out var w) ? w : null,
                        snapshotVersions.TryGetValue(c.Id, out var v) ? v : null))
                    .ToList(),
                Groups = groups
            };
            Summarize(dto);
            return Ok(dto);
        }

        private async Task<PowerAcKpiResultsDto> ToDtoAsync(PowerAcCalculationContext context, bool isPreview, DateTime? calculatedAt, string? calculatedBy)
        {
            var regionIds = context.Groups.Where(g => g.RegionDataId.HasValue).Select(g => g.RegionDataId!.Value).ToList();
            var regions = await _db.RegionData.AsNoTracking().Where(r => regionIds.Contains(r.Id)).ToListAsync();

            var dto = new PowerAcKpiResultsDto
            {
                Year = context.Period.Year,
                PeriodType = context.Period.PeriodType,
                PeriodNumber = context.Period.PeriodNumber,
                IsPreview = isPreview,
                CalculatedAt = calculatedAt,
                CalculatedBy = calculatedBy,
                PointsMethod = context.Settings.PointsMethod,
                CompositeTarget = context.Settings.CompositeTarget,
                PointsMethodApproved = context.Settings.PointsMethodApproved,
                RequireVerifiedData = context.Settings.RequireVerifiedData,
                PointsApplicable = context.Settings.PointsApplicable,
                TotalAllocation = context.Output.TotalAllocation,
                AllocationsComplete = context.Output.AllocationsComplete,
                WeightsValid = context.Output.WeightsValid,
                WeightTotal = context.Output.WeightTotal,
                GlobalIssues = context.Output.GlobalIssues.Distinct().ToList(),
                Components = context.Components.Select(c => PowerAcConfigController.ToDto(c,
                    context.ComponentConfigs.FirstOrDefault(x => x.ComponentId == c.Id)?.WeightPercent,
                    context.Versions.TryGetValue(c.Id, out var version) ? version : null)).ToList(),
                Groups = context.Output.Groups.Select(g =>
                {
                    var group = context.Groups.First(x => x.Id == g.EngineerGroupId);
                    var region = regions.FirstOrDefault(r => r.Id == group.RegionDataId);
                    return new PowerAcGroupResultDto
                    {
                        EngineerGroupId = g.EngineerGroupId,
                        EngineerGroupCode = g.Code,
                        WorkbookLabel = group.WorkbookLabel,
                        NetworkEngineer = region?.NetworkEngineer,
                        LeaCode = region?.LeaCode,
                        Status = g.Status,
                        CompositeAchievement = g.CompositeAchievement,
                        PointsApplicable = g.PointsApplicable,
                        AllocationValue = g.AllocationValue,
                        TotalAllocation = g.TotalAllocation,
                        MaximumPoints = g.MaximumPoints,
                        PointsAchieved = g.PointsAchieved,
                        Issues = g.Issues.Distinct().ToList(),
                        Warnings = g.Warnings.Distinct().ToList(),
                        Components = g.Components.Select(c => new PowerAcComponentScoreDto
                        {
                            ComponentId = c.ComponentId,
                            Code = c.Code,
                            Name = c.Name,
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
                            Message = c.Message
                        }).ToList()
                    };
                }).ToList()
            };
            Summarize(dto);
            return dto;
        }

        private static void Summarize(PowerAcKpiResultsDto dto)
        {
            dto.CompleteCount = dto.Groups.Count(g => g.Status == PowerAcResultStatus.Complete);
            dto.IncompleteCount = dto.Groups.Count(g => g.Status == PowerAcResultStatus.Incomplete);
            dto.PendingCount = dto.Groups.Count(g => g.Status == PowerAcResultStatus.Pending);
            dto.TotalMaximumPoints = dto.Groups.Any(g => g.MaximumPoints != null) ? dto.Groups.Sum(g => g.MaximumPoints ?? 0m) : null;
            dto.TotalPointsAchieved = dto.Groups.Any(g => g.PointsAchieved != null) ? dto.Groups.Sum(g => g.PointsAchieved ?? 0m) : null;
        }

        private static (List<string> Issues, List<string> Warnings) ParseIssues(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (new List<string>(), new List<string>());
            try
            {
                using var doc = JsonDocument.Parse(json);
                List<string> Read(string name) => doc.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array
                    ? el.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x != string.Empty).ToList()
                    : new List<string>();
                return (Read("issues"), Read("warnings"));
            }
            catch (JsonException)
            {
                return (new List<string> { json }, new List<string>());
            }
        }
    }
}
