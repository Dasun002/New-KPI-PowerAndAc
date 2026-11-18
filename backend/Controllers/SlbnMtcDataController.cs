using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/slbn-mtc-data")]
    [Authorize]
    public class SlbnMtcDataController : ControllerBase
    {
        private static readonly Dictionary<string, int> MonthSequence =
            CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select((name, index) => new { Name = name, Number = index + 1 })
                .ToDictionary(x => x.Name, x => x.Number, StringComparer.OrdinalIgnoreCase);

        private readonly AppDbContext _db;
        private readonly IAuthorizationService _authorizationService;
        private readonly ISlbnMtcDataCumulativeService _cumulativeService;
        private const int PageId = 6;

        public SlbnMtcDataController(
            AppDbContext db,
            IAuthorizationService authorizationService,
            ISlbnMtcDataCumulativeService cumulativeService)
        {
            _db = db;
            _authorizationService = authorizationService;
            _cumulativeService = cumulativeService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? designation, [FromQuery] int? year)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            var query = _db.SlbnMtcData.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(designation))
            {
                var normalizedDesignation = designation.Trim();
                query = query.Where(x => x.Designation != null && x.Designation.Trim() == normalizedDesignation);
            }

            if (year.HasValue)
            {
                query = query.Where(x => x.Year == year.Value);
            }

            var rows = await query.ToListAsync();

            return Ok(rows
                .OrderBy(x => x.Designation)
                .ThenBy(x => x.Year)
                .ThenBy(x => GetMonthNumber(x.Month))
                .Select(ToDto));
        }

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] UpsertSlbnMtcDataDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var authResult = await AuthorizeEditAsync();
            if (!authResult.Succeeded) return Forbid();

            var normalized = NormalizeRequest(dto);
            if (normalized.Error != null) return BadRequest(normalized.Error);

            var candidates = await _db.SlbnMtcData
                .Where(x =>
                    x.Designation != null &&
                    x.Designation.Trim() == normalized.Designation &&
                    x.Year == normalized.Year)
                .ToListAsync();

            var entity = candidates
                .OrderBy(x => x.Id)
                .FirstOrDefault(x => GetMonthNumber(x.Month) == normalized.MonthNumber);
            var isNew = entity == null;

            if (isNew)
            {
                entity = new SlbnMtcData
                {
                    Designation = normalized.Designation,
                    Year = normalized.Year,
                    Month = normalized.Month,
                    Scheduled = normalized.Scheduled,
                    Attended = normalized.Attended,
                    CumulativeSched = 0,
                    CumulativeAchieved = 0
                };

                _db.SlbnMtcData.Add(entity);
            }
            else
            {
                entity!.Designation = normalized.Designation;
                entity.Year = normalized.Year;
                entity.Month = normalized.Month;
                entity.Scheduled = normalized.Scheduled;
                entity.Attended = normalized.Attended;
            }

            await _db.SaveChangesAsync();
            await _cumulativeService.RecalculateAsync(normalized.Designation, normalized.Year);

            return Ok(new { id = entity.Id, isNew });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpsertSlbnMtcDataDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var authResult = await AuthorizeEditAsync();
            if (!authResult.Succeeded) return Forbid();

            var entity = await _db.SlbnMtcData.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            var originalDesignation = entity.Designation?.Trim();
            var originalYear = entity.Year;

            var normalized = NormalizeRequest(dto);
            if (normalized.Error != null) return BadRequest(normalized.Error);

            var duplicateCandidates = await _db.SlbnMtcData
                .AsNoTracking()
                .Where(x =>
                    x.Id != id &&
                    x.Designation != null &&
                    x.Designation.Trim() == normalized.Designation &&
                    x.Year == normalized.Year)
                .ToListAsync();

            if (duplicateCandidates.Any(x => GetMonthNumber(x.Month) == normalized.MonthNumber))
            {
                return Conflict("A record already exists for the same Designation, Year, and Month.");
            }

            entity.Designation = normalized.Designation;
            entity.Year = normalized.Year;
            entity.Month = normalized.Month;
            entity.Scheduled = normalized.Scheduled;
            entity.Attended = normalized.Attended;

            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(originalDesignation) && originalYear.HasValue &&
                (!string.Equals(originalDesignation, normalized.Designation, StringComparison.OrdinalIgnoreCase) ||
                 originalYear.Value != normalized.Year))
            {
                await _cumulativeService.RecalculateAsync(originalDesignation, originalYear.Value);
            }

            await _cumulativeService.RecalculateAsync(normalized.Designation, normalized.Year);

            return Ok(new { id = entity.Id });
        }

        private async Task<AuthorizationResult> AuthorizeEditAsync()
        {
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
            {
                return AuthorizationResult.Success();
            }

            return await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
        }

        [HttpPatch("{id:int}/toggle-verified")]
        public async Task<IActionResult> ToggleVerified(int id)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            var entity = await _db.SlbnMtcData
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.IsVerified })
                .FirstOrDefaultAsync();
            if (entity == null) return NotFound();

            var newValue = !entity.IsVerified;

            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE dbo.slbnmtcdata SET [is_verified] = @p0 WHERE [id] = @p1",
                newValue,
                id);

            return Ok(new { id = entity.Id, isVerified = newValue });
        }

        private static SlbnMtcDataDto ToDto(SlbnMtcData entity) => new()
        {
            Id = entity.Id,
            Designation = entity.Designation ?? string.Empty,
            Year = entity.Year ?? 0,
            Month = entity.Month ?? string.Empty,
            Scheduled = entity.Scheduled ?? 0,
            Attended = entity.Attended ?? 0,
            CumulativeSched = entity.CumulativeSched,
            CumulativeAchieved = entity.CumulativeAchieved,
            IsVerified = entity.IsVerified
        };

        private static NormalizedSlbnRequest NormalizeRequest(UpsertSlbnMtcDataDto dto)
        {
            var designation = dto.Designation.Trim();
            if (string.IsNullOrWhiteSpace(designation))
            {
                return NormalizedSlbnRequest.Invalid("Designation is required.");
            }

            var month = dto.Month.Trim();
            if (!MonthSequence.TryGetValue(month, out var monthNumber))
            {
                return NormalizedSlbnRequest.Invalid("Month must be a full month name from January to December.");
            }

            var normalizedMonth = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(monthNumber);

            return new NormalizedSlbnRequest(
                designation,
                dto.Year,
                normalizedMonth,
                monthNumber,
                dto.Scheduled,
                dto.Attended,
                null);
        }

        private static int GetMonthNumber(string? month)
        {
            if (string.IsNullOrWhiteSpace(month)) return 0;
            return MonthSequence.TryGetValue(month.Trim(), out var monthNumber) ? monthNumber : 0;
        }

        private sealed record NormalizedSlbnRequest(
            string Designation,
            int Year,
            string Month,
            int MonthNumber,
            int Scheduled,
            int Attended,
            string? Error)
        {
            public static NormalizedSlbnRequest Invalid(string error) =>
                new(string.Empty, 0, string.Empty, 0, 0, 0, error);
        }
    }
}
