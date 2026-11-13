/*
 * File: PowerAcController.cs
 * Power & A/C KPI API on the single table dbo.PowerAcPmData. Same pattern as the other KPIs:
 *   GET  api/power-ac/data?year&month       values per engineer group
 *   PUT  api/power-ac/data                  save Achievement % and allocation (clears verification)
 *   POST api/power-ac/data/verify?year&month verify rows entered by someone else
 *   GET  api/power-ac/results?year&month    calculated results (rules in PowerAcCalculator)
 * Page 11 (Power & A/C) rights: view = ViewPagePolicy, edit = EditPlatformKpiPolicy or Admin/SuperAdmin,
 * verify = SuperAdmin or EditPlatformKpiPolicy, never the person who entered the row.
 */
using System.Security.Claims;
using backend.Data;
using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/power-ac")]
    [Authorize]
    public class PowerAcController : ControllerBase
    {
        private const int PageId = 11;
        private readonly AppDbContext _db;
        private readonly IAuthorizationService _authorization;

        public PowerAcController(AppDbContext db, IAuthorizationService authorization)
        {
            _db = db;
            _authorization = authorization;
        }

        public class DataRowDto
        {
            public string Designation { get; set; } = string.Empty;
            public string EngineerGroupCode { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            // Achievement % per configured KPI code.
            public Dictionary<string, decimal?> Achievements { get; set; } = new();
            public decimal? AreaAllocation { get; set; }
            public bool IsVerified { get; set; }
            public string? EnteredBy { get; set; }
            public string? VerifiedBy { get; set; }
        }

        public class SaveItemDto
        {
            public string Designation { get; set; } = string.Empty;
            // Achievement % per configured KPI code. A null value clears that KPI for the group.
            public Dictionary<string, decimal?> Achievements { get; set; } = new();
            public decimal? AreaAllocation { get; set; }
        }

        public class SaveDataDto
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public List<SaveItemDto> Items { get; set; } = new();
        }

        [HttpGet("data")]
        public async Task<ActionResult<List<DataRowDto>>> GetData([FromQuery] int year, [FromQuery] int month)
        {
            if (!await CanViewAsync()) return Forbid();
            if (month < 1 || month > 12) return BadRequest(new { message = "month must be between 1 and 12." });
            var components = await LoadComponentsAsync();
            var rows = await PeriodRowsAsync(year, month);
            return Ok(PowerAcConfig.Groups.Select(g =>
            {
                var r = rows.FirstOrDefault(x => x.Designation.Trim() == g.Designation);
                return new DataRowDto
                {
                    Designation = g.Designation,
                    EngineerGroupCode = g.Code,
                    Label = g.Label,
                    Achievements = components.ToDictionary(c => c.Code, c => r is null ? null : PowerAcCalculator.AchievementOf(r, c.Code)),
                    AreaAllocation = r?.AreaAllocation,
                    IsVerified = r?.IsVerified ?? false,
                    EnteredBy = r?.EnteredBy,
                    VerifiedBy = r?.VerifiedBy
                };
            }).ToList());
        }

        [HttpPut("data")]
        public async Task<IActionResult> SaveData([FromBody] SaveDataDto dto)
        {
            if (!await CanEditAsync()) return Forbid();
            if (dto.Month < 1 || dto.Month > 12) return BadRequest(new { message = "month must be between 1 and 12." });
            var known = PowerAcConfig.Groups.Select(g => g.Designation).ToHashSet();
            var unknown = dto.Items.Where(i => !known.Contains(i.Designation.Trim())).Select(i => i.Designation).ToList();
            if (unknown.Count > 0) return BadRequest(new { message = $"Unknown engineer groups: {string.Join(", ", unknown)}." });
            var codes = (await LoadComponentsAsync()).Select(c => c.Code).ToHashSet();
            foreach (var i in dto.Items)
            {
                var badCode = i.Achievements.Keys.FirstOrDefault(k => !codes.Contains(k));
                if (badCode != null) return BadRequest(new { message = $"Unknown Power & A/C KPI code: {badCode}." });
                foreach (var v in i.Achievements.Values)
                    if (v is < 0m or > 100m) return BadRequest(new { message = "Achievement % must be between 0 and 100." });
                if (i.AreaAllocation is < 0m) return BadRequest(new { message = "Area allocation cannot be negative." });
            }

            var user = CurrentUser();
            var now = DateTime.UtcNow;
            var year = dto.Year.ToString();
            var monthName = PowerAcConfig.MonthNames[dto.Month - 1];
            var existing = await _db.PowerAcPmData
                .Where(r => r.Year == year && r.Month == monthName)
                .ToListAsync();

            foreach (var item in dto.Items)
            {
                var row = existing.FirstOrDefault(r => r.Designation.Trim() == item.Designation.Trim());
                if (row is null)
                {
                    row = new PowerAcPmData
                    {
                        Designation = item.Designation.Trim(),
                        Year = year,
                        Month = monthName,
                        Cumulative_Sched = 0,
                        Cumulative_Achieved = 0,
                        EnteredBy = user,
                    };
                    _db.PowerAcPmData.Add(row);
                }
                foreach (var code in codes)
                    PowerAcCalculator.SetAchievement(row, code, item.Achievements.TryGetValue(code, out var v) ? v : null);
                row.AreaAllocation = item.AreaAllocation;
                row.IsVerified = false;
                row.VerifiedBy = null;
                row.EnteredBy = user;
                row.UpdatedAt = now;
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "Power & A/C values saved.", saved = dto.Items.Count });
        }

        [HttpPost("data/verify")]
        public async Task<IActionResult> Verify([FromQuery] int year, [FromQuery] int month)
        {
            if (!await CanVerifyAsync()) return Forbid();
            if (month < 1 || month > 12) return BadRequest(new { message = "month must be between 1 and 12." });
            var user = CurrentUser();
            var rows = (await PeriodRowsAsync(year, month, track: true))
                .Where(r => !r.IsVerified && r.PmAchievement != null && r.AreaAllocation != null)
                .ToList();
            var verifiable = rows.Where(r => !string.Equals(r.EnteredBy, user, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var r in verifiable)
            {
                r.IsVerified = true;
                r.VerifiedBy = user;
            }
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = $"{verifiable.Count} row(s) verified." + (rows.Count > verifiable.Count ? " Rows you entered cannot be verified by you." : ""),
                verified = verifiable.Count,
                skipped = rows.Count - verifiable.Count
            });
        }

        // Toggles verification of one engineer group's row for the month (the ✓/✗ on the overview).
        [HttpPatch("data/toggle-verified")]
        public async Task<IActionResult> ToggleVerified([FromQuery] int year, [FromQuery] int month, [FromQuery] string designation)
        {
            if (!await CanVerifyAsync()) return Forbid();
            if (month < 1 || month > 12) return BadRequest(new { message = "month must be between 1 and 12." });
            var y = year.ToString();
            var m = PowerAcConfig.MonthNames[month - 1];
            var d = designation.Trim();
            var row = await _db.PowerAcPmData.FirstOrDefaultAsync(r => r.Year == y && r.Month == m && r.Designation.Trim() == d);
            if (row is null) return NotFound(new { message = "No row for this engineer group and month." });

            var user = CurrentUser();
            if (!row.IsVerified)
            {
                if (string.Equals(row.EnteredBy, user, StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { message = "You cannot verify values you entered." });
                row.IsVerified = true;
                row.VerifiedBy = user;
            }
            else
            {
                row.IsVerified = false;
                row.VerifiedBy = null;
            }
            await _db.SaveChangesAsync();
            return Ok(new { designation = d, isVerified = row.IsVerified });
        }

        [HttpGet("results")]
        public async Task<ActionResult<object>> GetResults([FromQuery] int year, [FromQuery] int month)
        {
            if (!await CanViewAsync()) return Forbid();
            if (month < 1 || month > 12) return BadRequest(new { message = "month must be between 1 and 12." });
            var rows = await PeriodRowsAsync(year, month);
            var pointsApplicable = await _db.KpiDefinitions.AsNoTracking()
                .Where(k => k.Id == PowerAcConfig.PointsKpiDefinitionId)
                .Select(k => (decimal?)k.PointsApplicable)
                .FirstOrDefaultAsync();
            var components = await LoadComponentsAsync();
            var results = PowerAcCalculator.Calculate(rows, components, pointsApplicable);
            var totalAllocation = results.All(r => r.AllocationValue is not null) ? results.Sum(r => r.AllocationValue!.Value) : (decimal?)null;
            return Ok(new
            {
                year,
                month,
                pointsApplicable,
                totalAllocation,
                completeCount = results.Count(r => r.Status == "Complete"),
                incompleteCount = results.Count(r => r.Status != "Complete"),
                totalMaximumPoints = results.Any(r => r.MaximumPoints != null) ? results.Sum(r => r.MaximumPoints ?? 0m) : (decimal?)null,
                totalPointsAchieved = results.Any(r => r.PointsAchieved != null) ? results.Sum(r => r.PointsAchieved ?? 0m) : (decimal?)null,
                components = components.Select(c => new
                {
                    code = c.Code,
                    name = c.Name,
                    weightPercent = c.WeightPercent,
                    target = c.Target,
                    targetPercent = c.TargetValue
                }),
                groups = results.Select(r => new
                {
                    code = r.Code,
                    label = r.Label,
                    designation = r.Designation,
                    achievement = r.Achievement,
                    allocationValue = r.AllocationValue,
                    isVerified = r.IsVerified,
                    composite = r.Composite,
                    maximumPoints = r.MaximumPoints,
                    pointsAchieved = r.PointsAchieved,
                    status = r.Status,
                    issues = r.Issues
                })
            });
        }

        // Read-only queries use AsNoTracking. Verify reads tracked rows so its changes are saved.
        // The Power & A/C KPIs are KpiDefinition rows with Category = PowerAndAC (code in StrategicObjectives).
        public static async Task<List<PowerAcConfig.Component>> LoadComponentsAsync(AppDbContext db)
        {
            var rows = await db.KpiDefinitions.AsNoTracking()
                .Where(k => k.Category == PowerAcConfig.Category)
                .OrderBy(k => k.Id)
                .ToListAsync();
            return rows.Select(k => PowerAcConfig.ToComponent(k)).ToList();
        }

        private Task<List<PowerAcConfig.Component>> LoadComponentsAsync() => LoadComponentsAsync(_db);

        private async Task<List<PowerAcPmData>> PeriodRowsAsync(int year, int month, bool track = false)
        {
            var y = year.ToString();
            var m = PowerAcConfig.MonthNames[month - 1];
            var query = track ? _db.PowerAcPmData.AsQueryable() : _db.PowerAcPmData.AsNoTracking();
            return await query.Where(r => r.Year == y && r.Month == m).ToListAsync();
        }

        private string CurrentUser() =>
            User.FindFirst("serviceId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "unknown";

        private static bool HasRole(ClaimsPrincipal user, string role) =>
            user.Claims.Any(c => (c.Type == "role" || c.Type == ClaimTypes.Role) && string.Equals(c.Value?.Replace(" ", string.Empty), role, StringComparison.OrdinalIgnoreCase));

        private async Task<bool> CanViewAsync() =>
            (await _authorization.AuthorizeAsync(User, PageId, "ViewPagePolicy")).Succeeded;

        private async Task<bool> CanEditAsync() =>
            HasRole(User, "Admin") || HasRole(User, "SuperAdmin")
            || (await _authorization.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy")).Succeeded;

        private async Task<bool> CanVerifyAsync() =>
            HasRole(User, "SuperAdmin") || (await _authorization.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy")).Succeeded;
    }
}
