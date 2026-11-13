using backend.Services.PowerAc;
using backend.Data;
using backend.DTOs;
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


            results.AddRange(await PowerAcRowsAsync(year, startMonth, endMonth));

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

        // Power & A/C rows: one row per KPI and area, calculated from the single Power & A/C table.
        // KpiDefinitionId is -(position of the KPI in configuration order + 1), as the analytics page expects.
        private async Task<List<AnalyticsResultDto>> PowerAcRowsAsync(short year, byte startMonth, byte endMonth)
        {
            var components = await PowerAcKpiLoader.LoadScoredAsync(_db);
            if (components.Count == 0) return new List<AnalyticsResultDto>();

            var pointsApplicable = await _db.KpiDefinitions.AsNoTracking()
                .Where(k => k.Id == PowerAcConfig.PointsKpiDefinitionId)
                .Select(k => (decimal?)k.PointsApplicable)
                .FirstOrDefaultAsync();
            var regions = await _db.RegionData.AsNoTracking().ToListAsync();
            // Same sharing as the 2026-Q1 export: each KPI's share is its weight divided by the total of the configured weights.
            var totalWeight = components.Sum(c => c.WeightPercent);
            if (totalWeight <= 0m) totalWeight = 100m;
            var y = year.ToString();
            var rows = new List<AnalyticsResultDto>();

            for (var month = startMonth; month <= endMonth; month++)
            {
                var monthName = PowerAcConfig.MonthNames[month - 1];
                var data = await _db.PowerAcPmData.AsNoTracking()
                    .Where(r => r.Year == y && r.Month == monthName)
                    .ToListAsync();
                foreach (var group in PowerAcCalculator.Calculate(data, components, pointsApplicable))
                {
                    var area = regions.FirstOrDefault(r => r.NetworkEngineer == group.Code)?.LeaCode ?? string.Empty;
                    if (area.Length == 0) continue;
                    for (var i = 0; i < components.Count; i++)
                    {
                        var component = components[i];
                        var achievement = group.Achievement.TryGetValue(component.Code, out var a) ? a : null;
                        if (achievement is null || group.MaximumPoints is null) continue;
                        var maxPoints = group.MaximumPoints.Value * component.WeightPercent / totalWeight;
                        rows.Add(new AnalyticsResultDto
                        {
                            KpiDefinitionId = -(i + 1),
                            ComponentCode = component.Code,
                            KpiName = component.Name,
                            AreaCode = area,
                            Year = year,
                            AchievedKpi = achievement.Value,
                            MaximumPointsPerKpi = Math.Round(maxPoints, 4),
                            PointsAchieved = Math.Round(maxPoints * achievement.Value / 100m, 4),
                            OverallKpiValuePercent = 0m
                        });
                    }
                }
            }
            return rows
                .GroupBy(r => new { r.KpiDefinitionId, Area = r.AreaCode.Trim().ToLowerInvariant() })
                .Select(g => new AnalyticsResultDto
                {
                    KpiDefinitionId = g.Key.KpiDefinitionId,
                    ComponentCode = g.First().ComponentCode,
                    KpiName = g.First().KpiName,
                    AreaCode = g.First().AreaCode,
                    Year = year,
                    AchievedKpi = g.Average(x => x.AchievedKpi),
                    MaximumPointsPerKpi = g.Sum(x => x.MaximumPointsPerKpi),
                    PointsAchieved = g.Sum(x => x.PointsAchieved),
                    OverallKpiValuePercent = 0m
                })
                .ToList();
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
