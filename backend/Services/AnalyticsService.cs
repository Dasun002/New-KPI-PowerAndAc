using backend.Data;
using backend.DTOs;
using backend.Models.PowerAc;
using Microsoft.EntityFrameworkCore;

namespace backend.Services
{
    public class AnalyticsService
    {
        private readonly AppDbContext _db;

        public AnalyticsService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<AnalyticsResultDto>> GetCumulativeAnalyticsAsync(short year, byte startMonth, byte endMonth)
        {
            var rows = await _db.OverallKpiResults
                .AsNoTracking()
                .Where(x => x.Year == year && x.Month >= startMonth && x.Month <= endMonth)
                .ToListAsync();

            var results = rows
                .Where(x => !IsLegacyPowerAcRow(x.KpiName))
                .GroupBy(x => new
                {
                    x.KpiDefinitionId,
                    NormalizedAreaCode = (x.AreaCode ?? string.Empty).Trim().ToLowerInvariant()
                })
                .Select(g => new AnalyticsResultDto
                {
                    KpiDefinitionId = g.Key.KpiDefinitionId,
                    AreaCode = g.First().AreaCode,
                    KpiName = g.Max(x => x.KpiName),
                    Year = year,
                    AchievedKpi = g.Average(x => x.AchievedKpi),
                    MaximumPointsPerKpi = g.Sum(x => x.MaximumPointsPerKpi),
                    PointsAchieved = g.Sum(x => x.PointsAchieved),
                    OverallKpiValuePercent = 0m
                })
                .OrderBy(x => x.KpiDefinitionId)
                .ThenBy(x => x.AreaCode)
                .ToList();

            var componentResults = await _db.PowerAcKpiResults
                .AsNoTracking()
                .Include(x => x.EngineerGroup!)
                    .ThenInclude(x => x.RegionData)
                .Include(x => x.ComponentScores)
                    .ThenInclude(x => x.Component)
                .Where(x => x.Year == year
                    && x.PeriodType == PowerAcPeriodTypes.Month
                    && x.PeriodNumber >= startMonth
                    && x.PeriodNumber <= endMonth
                    && x.Status == PowerAcResultStatus.Complete)
                .ToListAsync();

            var componentRows = componentResults
                .SelectMany(result => result.ComponentScores
                    .Where(score => score.Component is not null
                        && score.Status == PowerAcResultStatus.Complete
                        && score.Score.HasValue
                        && result.EngineerGroup?.RegionData is not null)
                    .Select(score => new
                    {
                        score.ComponentId,
                        ComponentCode = score.Component!.Code,
                        ComponentName = score.Component.Name,
                        AreaCode = result.EngineerGroup!.RegionData!.LeaCode,
                        AchievedKpi = score.Score!.Value * 100m,
                        MaximumPoints = result.MaximumPoints!.Value * ((score.WeightPercent ?? 0m) / 100m),
                        PointsAchieved = result.MaximumPoints!.Value * (score.WeightedScore ?? 0m)
                    }))
                .GroupBy(x => new { x.ComponentId, x.ComponentCode, x.ComponentName, Area = (x.AreaCode ?? string.Empty).Trim().ToLowerInvariant() })
                .Select(g => new AnalyticsResultDto
                {
                    KpiDefinitionId = -g.Key.ComponentId,
                    ComponentCode = g.Key.ComponentCode,
                    KpiName = g.Key.ComponentName,
                    AreaCode = g.First().AreaCode,
                    Year = year,
                    AchievedKpi = g.Average(x => x.AchievedKpi),
                    MaximumPointsPerKpi = g.Sum(x => x.MaximumPoints),
                    PointsAchieved = g.Sum(x => x.PointsAchieved),
                    OverallKpiValuePercent = 0m
                })
                .ToList();

            results.AddRange(componentRows);

            var overallPercentByArea = results
                .GroupBy(x => x.AreaCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var totalMax = g.Sum(x => x.MaximumPointsPerKpi);
                        var totalAchieved = g.Sum(x => x.PointsAchieved);
                        return totalMax > 0m
                            ? Math.Round((totalAchieved / totalMax) * 100m, 4)
                            : 0m;
                    },
                    StringComparer.OrdinalIgnoreCase);

            foreach (var row in results)
            {
                if (overallPercentByArea.TryGetValue(row.AreaCode, out var overallPercent))
                {
                    row.OverallKpiValuePercent = overallPercent;
                }
            }

            return results;
        }

        private static bool IsLegacyPowerAcRow(string? name)
        {
            var normalized = (name ?? string.Empty).Replace("&", "and", StringComparison.OrdinalIgnoreCase);
            return normalized.Contains("power", StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("air", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<int>> GetAvailableYearsAsync()
        {
            return await _db.OverallKpiResults
                .AsNoTracking()
                .Select(x => (int)x.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
        }

        public async Task<List<int>> GetAvailableMonthsAsync(short year)
        {
            return await _db.OverallKpiResults
                .AsNoTracking()
                .Where(x => x.Year == year)
                .Select(x => (int)x.Month)
                .Distinct()
                .OrderByDescending(m => m)
                .ToListAsync();
        }
    }
}
