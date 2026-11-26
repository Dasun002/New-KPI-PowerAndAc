/*
 * File: PowerAcController.cs
 * Power & A/C KPI API on the single table dbo.PowerAcPmData. Same pattern as the other KPIs:
 *   GET  api/power-ac/data?year&month       values per engineer group
 *   PUT  api/power-ac/data                  save Achievement %, Safety incident count, Availability % and allocation
 *                                          (clears verification; Safety and Availability scores are calculated on save)
 * POST api/power-ac/data/verify?year&month verify groups that have all required values (others are reported)
 *   PATCH api/power-ac/data/toggle-verified  verify one group (all required values must be present) or remove verification
 *   GET  api/power-ac/results?year&month    calculated results (rules in PowerAcCalculator)
 * Page 11 (Power & A/C) rights: view = ViewPagePolicy; edit and verify require an explicit
 * Power & A/C platform assignment. Assigned users may verify their own entries.
 */
using System.Security.Claims;
using backend.Data;
using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
        private readonly IConfiguration _configuration;

        public PowerAcController(AppDbContext db, IAuthorizationService authorization, IConfiguration configuration)
        {
            _db = db;
            _authorization = authorization;
            _configuration = configuration;
        }

        // Settings for the rule-scored KPIs. Read on each save, so a change applies to rows saved after it.
        private decimal SafetyDeductionPerIncident =>
            _configuration.GetValue("PowerAc:SafetyDeductionPerIncident", PowerAcScoring.DefaultSafetyDeductionPerIncident);

        private decimal AvailabilityTargetPercent =>
            _configuration.GetValue("PowerAc:AvailabilityTargetPercent", PowerAcScoring.DefaultAvailabilityTargetPercent);

        public class DataRowDto
        {
            public string Designation { get; set; } = string.Empty;
            public string EngineerGroupCode { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            // Achievement % per configured KPI code (calculated score for the rule-scored KPIs).
            public Dictionary<string, decimal?> Achievements { get; set; } = new();
            // Typed inputs for the rule-scored KPIs: SAFETY = incident count, AVAILABILITY = actual %.
            public Dictionary<string, decimal?> Inputs { get; set; } = new();
            // Rule-scored KPIs held as an old percentage only (no typed input). See PowerAcCalculator.LegacyCodesOf.
            public List<string> LegacyCodes { get; set; } = new();
            // Verification status and the requirements still missing (see PowerAcCalculator.VerificationStatusOf).
            public string VerificationStatus { get; set; } = string.Empty;
            public List<string> VerificationMissing { get; set; } = new();
            public decimal? AreaAllocation { get; set; }
            public bool IsVerified { get; set; }
            public string? EnteredBy { get; set; }
            public string? VerifiedBy { get; set; }
        }

        public class SaveItemDto
        {
            public string Designation { get; set; } = string.Empty;
            // Achievement % per KPI code. A key that is not sent keeps its saved value; a key sent with null clears it.
            // Do not send SAFETY or AVAILABILITY here: they are saved from Inputs.
            public Dictionary<string, decimal?> Achievements { get; set; } = new();
            // Typed inputs for the rule-scored KPIs. Same rule: not sent = unchanged, null = cleared.
            //   SAFETY       = incident count (whole number, 0 or more)
            //   AVAILABILITY = actual availability % (0 to 100)
            public Dictionary<string, decimal?> Inputs { get; set; } = new();

            // Area allocation. Omitted (not sent) keeps the saved value; null clears it; a number sets it.
            private decimal? _areaAllocation;
            public decimal? AreaAllocation
            {
                get => _areaAllocation;
                set { _areaAllocation = value; AreaAllocationSpecified = true; }
            }

            [System.Text.Json.Serialization.JsonIgnore]
            public bool AreaAllocationSpecified { get; private set; }
        }

        public class SaveDataDto
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public List<SaveItemDto> Items { get; set; } = new();
        }

        public class MonthlyCellDto
        {
            public int Month { get; set; }
            public string Designation { get; set; } = string.Empty;
            // Distribution: number scheduled. Achievement: number attended.
            public int? Scheduled { get; set; }
            public int? Attended { get; set; }
            // True when the month has a row for this group (verification can only be set on such a row).
            public bool HasRow { get; set; }
            public bool IsVerified { get; set; }
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
                    Inputs = new Dictionary<string, decimal?>
                    {
                        [PowerAcScoring.SafetyCode] = r is null || r.SafetyIncidentCount is null ? null : r.SafetyIncidentCount,
                        [PowerAcScoring.AvailabilityCode] = r?.AvailabilityActualPct,
                    },
                    LegacyCodes = r is null ? new List<string>() : PowerAcCalculator.LegacyCodesOf(r),
                    VerificationStatus = r is null ? PowerAcCalculator.StatusIncomplete : PowerAcCalculator.VerificationStatusOf(r),
                    VerificationMissing = r is null ? PowerAcCalculator.MissingVerificationInputs(new PowerAcPmData()) : PowerAcCalculator.MissingVerificationInputs(r),
                    AreaAllocation = r?.AreaAllocation,
                    IsVerified = r?.IsVerified ?? false,
                    EnteredBy = r?.EnteredBy,
                    VerifiedBy = r?.VerifiedBy
                };
            }).ToList());
        }

        private static bool IsRuleCode(string code) =>
            code == PowerAcScoring.SafetyCode || code == PowerAcScoring.AvailabilityCode;

        private static string KpiDisplayName(string code) =>
            code == PowerAcScoring.SafetyCode ? "Critical Safety" : "Power System Availability";

        // Everything that a save can change. Used to tell a real change from a save that repeats the stored values.
        private static (decimal?, decimal?, decimal?, decimal?, string?, int?, decimal?, decimal?) Snapshot(PowerAcPmData r) =>
            (r.PmAchievement, r.SafetyAchievement, r.CorrectiveAchievement, r.AvailabilityAchievement,
             r.AchievementsJson, r.SafetyIncidentCount, r.AvailabilityActualPct, r.AreaAllocation);

        [HttpPut("data")]
        public async Task<IActionResult> SaveData([FromBody] SaveDataDto dto)
        {
            if (!await CanEditAsync()) return Forbid();
            if (dto is null) return BadRequest(new { message = "The request body is required." });
            if (dto.Month < 1 || dto.Month > 12) return BadRequest(new { message = "month must be between 1 and 12." });
            var known = PowerAcConfig.Groups.Select(g => g.Designation).ToHashSet();
            var unknown = dto.Items.Where(i => !known.Contains(i.Designation.Trim())).Select(i => i.Designation).ToList();
            if (unknown.Count > 0) return BadRequest(new { message = $"Unknown engineer groups: {string.Join(", ", unknown)}." });
            var codes = (await LoadComponentsAsync()).Select(c => c.Code).ToHashSet();

            foreach (var i in dto.Items)
            {
                var achievements = i.Achievements ?? new Dictionary<string, decimal?>();
                var inputs = i.Inputs ?? new Dictionary<string, decimal?>();

                var badCode = achievements.Keys.FirstOrDefault(k => !codes.Contains(k));
                if (badCode != null) return BadRequest(new { message = $"Unknown Power & A/C KPI code: {badCode}." });
                var ruleCode = achievements.Keys.FirstOrDefault(IsRuleCode);
                if (ruleCode != null) return BadRequest(new { message = $"{KpiDisplayName(ruleCode)} is entered as an input, not as Achievement %." });
                foreach (var v in achievements.Values)
                    if (v is < 0m or > 100m) return BadRequest(new { message = "Achievement % must be between 0 and 100." });

                foreach (var key in inputs.Keys)
                {
                    if (!IsRuleCode(key)) return BadRequest(new { message = $"Unknown input for KPI code: {key}." });
                    if (!codes.Contains(key))
                        return BadRequest(new { message = $"{KpiDisplayName(key)} is not configured as a Power & A/C KPI, so its input cannot be saved. Configure it on the Power & A/C configuration page first." });
                }
                if (inputs.TryGetValue(PowerAcScoring.SafetyCode, out var incidents) && incidents is not null)
                {
                    if (incidents.Value < 0m || incidents.Value > PowerAcScoring.MaxIncidentCount || incidents.Value != decimal.Truncate(incidents.Value))
                        return BadRequest(new { message = $"Critical Safety incidents must be a whole number from 0 to {PowerAcScoring.MaxIncidentCount}." });
                }
                if (inputs.TryGetValue(PowerAcScoring.AvailabilityCode, out var availability) && availability is not null)
                {
                    if (availability.Value < 0m || availability.Value > 100m)
                        return BadRequest(new { message = "Power System Availability % must be between 0 and 100." });
                }
                if (i.AreaAllocationSpecified && i.AreaAllocation is < 0m)
                    return BadRequest(new { message = "Area allocation cannot be negative." });
            }

            // Scoring settings are only needed when a rule-scored input is saved. An invalid setting is reported, never scored as zero.
            PowerAcScoring.Settings? settings = null;
            if (dto.Items.Any(i => (i.Inputs ?? new Dictionary<string, decimal?>()).Keys.Any(IsRuleCode)))
            {
                try
                {
                    settings = PowerAcScoring.LoadSettings(_configuration);
                }
                catch (InvalidOperationException ex)
                {
                    return StatusCode(500, new { message = $"Power & A/C scoring configuration is invalid: {ex.Message}" });
                }
            }

            var user = CurrentUser();
            var now = DateTime.UtcNow;
            var year = dto.Year.ToString();
            var monthName = PowerAcConfig.MonthNames[dto.Month - 1];
            var existing = await _db.PowerAcPmData
                .Where(r => r.Year == year && r.Month == monthName)
                .ToListAsync();

            var changedCount = 0;
            foreach (var item in dto.Items)
            {
                var achievements = item.Achievements ?? new Dictionary<string, decimal?>();
                var inputs = item.Inputs ?? new Dictionary<string, decimal?>();
                var row = existing.FirstOrDefault(r => r.Designation.Trim() == item.Designation.Trim());
                var isNew = row is null;
                if (row is null)
                {
                    row = new PowerAcPmData
                    {
                        Designation = item.Designation.Trim(),
                        Year = year,
                        Month = monthName,
                        Cumulative_Sched = 0,
                        Cumulative_Achieved = 0,
                    };
                    _db.PowerAcPmData.Add(row);
                }
                var before = Snapshot(row);

                // Achievement %: each key that was sent is written (null clears it); keys not sent keep their saved value.
                foreach (var (code, value) in achievements)
                    PowerAcCalculator.SetAchievement(row, code, value);

                // Rule-scored KPIs: a sent input is stored, and its score is calculated and stored beside it.
                if (inputs.TryGetValue(PowerAcScoring.SafetyCode, out var safetyInput))
                {
                    row.SafetyIncidentCount = safetyInput is null ? null : (int)safetyInput.Value;
                    PowerAcCalculator.SetAchievement(row, PowerAcScoring.SafetyCode,
                        safetyInput is null ? null : PowerAcScoring.SafetyAchievement((int)safetyInput.Value, settings!.SafetyDeductionPerIncident));
                }
                if (inputs.TryGetValue(PowerAcScoring.AvailabilityCode, out var availabilityInput))
                {
                    row.AvailabilityActualPct = availabilityInput;
                    PowerAcCalculator.SetAchievement(row, PowerAcScoring.AvailabilityCode,
                        availabilityInput is null ? null : PowerAcScoring.AvailabilityAchievement(availabilityInput.Value, settings!.AvailabilityTargetPercent));
                }

                if (item.AreaAllocationSpecified) row.AreaAllocation = item.AreaAllocation;

                // A save that repeats the stored values changes nothing: keep verification and the audit fields.
                if (!isNew && Snapshot(row).Equals(before)) continue;

                changedCount++;
                row.EnteredBy = user;
                row.UpdatedAt = now;
                row.IsVerified = false;
                row.VerifiedBy = null;
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "Power & A/C values saved.", saved = changedCount, unchanged = dto.Items.Count - changedCount });
        }

        // Monthly distribution (scheduled) and achievement (attended) counts per engineer group for a whole year.
        [HttpGet("data/monthly")]
        public async Task<ActionResult<List<MonthlyCellDto>>> GetMonthly([FromQuery] int year)
        {
            if (!await CanViewAsync()) return Forbid();
            var yearText = year.ToString();
            var rows = await _db.PowerAcPmData.AsNoTracking().Where(r => r.Year == yearText).ToListAsync();
            var cells = new List<MonthlyCellDto>();
            for (var month = 1; month <= 12; month++)
            {
                var monthName = PowerAcConfig.MonthNames[month - 1];
                foreach (var g in PowerAcConfig.Groups)
                {
                    var r = rows.FirstOrDefault(x => x.Month == monthName && x.Designation.Trim() == g.Designation);
                    cells.Add(new MonthlyCellDto
                    {
                        Month = month,
                        Designation = g.Designation,
                        Scheduled = r?.Scheduled,
                        Attended = r?.Attended,
                        HasRow = r is not null,
                        IsVerified = r?.IsVerified ?? false
                    });
                }
            }
            return Ok(cells);
        }

        // Verifies every not-yet-verified group in the period that has all required values. A group with a missing value
        // is not verified; it is returned in "issues" with the exact missing inputs. Groups with no values are not reported.
        [HttpPost("data/verify")]
        public async Task<IActionResult> Verify([FromQuery] int year, [FromQuery] int month)
        {
            if (!await CanVerifyAsync()) return Forbid();
            if (month < 1 || month > 12) return BadRequest(new { message = "month must be between 1 and 12." });
            var user = CurrentUser();
            var now = DateTime.UtcNow;
            var verified = new List<string>();
            var issues = new List<object>();
            var candidates = (await PeriodRowsAsync(year, month, track: true))
                .Where(r => !r.IsVerified && PowerAcCalculator.HasAnyVerificationInput(r))
                .OrderBy(r => r.Designation.Trim())
                .ToList();
            foreach (var r in candidates)
            {
                var designation = r.Designation.Trim();
                var missing = PowerAcCalculator.MissingVerificationInputs(r);
                if (missing.Count > 0)
                {
                    issues.Add(new { designation, missing, message = PowerAcCalculator.VerificationMessage(designation, missing) });
                    continue;
                }
                r.IsVerified = true;
                r.VerifiedBy = user;
                r.UpdatedAt = now;
                verified.Add(designation);
            }
            if (verified.Count > 0) await _db.SaveChangesAsync();
            return Ok(new
            {
                message = issues.Count == 0
                    ? $"{verified.Count} row(s) verified."
                    : $"{verified.Count} row(s) verified. {issues.Count} group(s) not verified because values are missing.",
                verified = verified.Count,
                skipped = issues.Count,
                verifiedDesignations = verified,
                issues
            });
        }

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
                var missing = PowerAcCalculator.MissingVerificationInputs(row);
                if (missing.Count > 0)
                    return BadRequest(new { message = PowerAcCalculator.VerificationMessage(d, missing), designation = d, missing });
                row.IsVerified = true;
                row.VerifiedBy = user;
            }
            else
            {
                row.IsVerified = false;
                row.VerifiedBy = null;
            }
            row.UpdatedAt = DateTime.UtcNow;
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
                    issues = r.Issues,
                    legacyCodes = r.LegacyCodes
                })
            });
        }

        // Read-only. Lists the groups that are verified but no longer meet the requirements (for example legacy percentages),
        // so a verifier can unverify them one at a time and re-enter the values. Nothing is changed by this call.
        [HttpGet("verification-review")]
        public async Task<ActionResult<object>> GetVerificationReview([FromQuery] int year, [FromQuery] int month)
        {
            if (!await CanViewAsync()) return Forbid();
            if (month < 1 || month > 12) return BadRequest(new { message = "month must be between 1 and 12." });
            var rows = await PeriodRowsAsync(year, month);
            var items = rows
                .Where(r => r.IsVerified && PowerAcCalculator.VerificationStatusOf(r) == PowerAcCalculator.StatusVerifiedNeedsReentry)
                .OrderBy(r => r.Designation.Trim())
                .Select(r => new
                {
                    designation = r.Designation.Trim(),
                    verifiedBy = r.VerifiedBy,
                    verifiedAt = r.UpdatedAt,
                    legacyCodes = PowerAcCalculator.LegacyCodesOf(r),
                    missing = PowerAcCalculator.MissingVerificationInputs(r),
                    action = "Unverify this group (click its ✓ or call toggle-verified), re-enter the missing values, then verify again."
                })
                .ToList();
            return Ok(new { year, month, count = items.Count, groups = items });
        }

        // For each month of the year: when its Power & A/C values last changed, and when the stored overall result was
        // last calculated. A month needs recalculating when the values changed after that calculation (or it was never done).
        [HttpGet("overall-status")]
        public async Task<ActionResult<object>> GetOverallStatus([FromQuery] int year)
        {
            if (!await CanViewAsync()) return Forbid();
            var yearText = year.ToString();
            var changes = await _db.PowerAcPmData.AsNoTracking()
                .Where(r => r.Year == yearText && r.UpdatedAt != null)
                .Select(r => new { r.Month, r.UpdatedAt })
                .ToListAsync();
            var calculations = await _db.OverallKpiResults.AsNoTracking()
                .Where(o => o.KpiDefinitionId == PowerAcConfig.PointsKpiDefinitionId && o.Year == (short)year)
                .Select(o => new { o.Month, o.CalculatedAt })
                .ToListAsync();

            var months = new List<object>();
            for (var month = 1; month <= 12; month++)
            {
                var monthName = PowerAcConfig.MonthNames[month - 1];
                DateTime? lastChanged = changes.Where(c => c.Month == monthName).Select(c => c.UpdatedAt).Max();
                DateTime? lastCalculated = calculations.Where(c => c.Month == month).Select(c => c.CalculatedAt).Max();
                var needsRecalculation = lastChanged.HasValue && (lastCalculated is null || lastChanged.Value > lastCalculated.Value);
                months.Add(new
                {
                    month,
                    lastValuesChanged = lastChanged,
                    lastOverallCalculated = lastCalculated,
                    needsRecalculation
                });
            }
            return Ok(new { year, months });
        }

        // Read-only queries use AsNoTracking. Verify reads tracked rows so its changes are saved.
        // The Power & A/C KPIs are KpiDefinition rows with Category = PowerAndAC (code in StrategicObjectives).
        public static Task<List<PowerAcConfig.Component>> LoadComponentsAsync(AppDbContext db) => PowerAcKpiLoader.LoadScoredAsync(db);

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

        private Task<bool> CanEditAsync() => Task.FromResult(HasPowerAcAssignment());

        private Task<bool> CanVerifyAsync() => Task.FromResult(HasPowerAcAssignment());

        private bool HasPowerAcAssignment() =>
            HasRole(User, "PlatformAdmin") &&
            User.FindAll("assignedKpiPages").Any(claim =>
                int.TryParse(claim.Value, out var assignedPageId) && assignedPageId == PageId);
    }
}
