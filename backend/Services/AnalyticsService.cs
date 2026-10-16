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
