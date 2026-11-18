/*
 * File: PowerAcKpiLoader.cs
 * Loads the Power & A/C KPIs that are part of the scored set (KpiDefinition, Category PowerAndAC, IncludeInScoring = 1).
 * Every calculation (Power & A/C page, analytics, overall scorecard) uses this one list.
 */
using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.PowerAc
{
    public static class PowerAcKpiLoader
    {
        public static async Task<List<PowerAcConfig.Component>> LoadScoredAsync(AppDbContext db)
        {
            var rows = await db.KpiDefinitions.AsNoTracking()
                .Where(k => k.Category == PowerAcConfig.Category && k.IncludeInScoring)
                .OrderBy(k => k.Id)
                .ToListAsync();
            return rows.Select(PowerAcConfig.ToComponent).ToList();
        }
    }
}
