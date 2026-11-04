using System;
using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OtherKpiController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationService _authorizationService;
        private const int PageId = 10;

        public OtherKpiController(AppDbContext db, IAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OtherKpiDto>>> GetAll()
        {
            var items = await _db.OtherKpis
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new OtherKpiDto
                {
                    Id = x.Id,
                    NetworkEngineerKpi = x.NetworkEngineerKpi,
                    Division = x.Division,
                    Section = x.Section,
                    KpiPercent = x.KpiPercent
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("metrics")]
        public async Task<ActionResult<IEnumerable<OtherKpiMetricDto>>> GetMetrics(
            [FromQuery] byte month,
            [FromQuery] short year,
            [FromQuery] string? areaCode,
            [FromQuery] string? site)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            if (month == 0 || year == 0)
                return BadRequest("Month and Year must be greater than zero.");

            var normalizedArea = NormalizeAreaCode(areaCode ?? site);

            var query =
                from metric in _db.OtherKpiMetrics.AsNoTracking()
                join kpi in _db.OtherKpis.AsNoTracking()
                    on metric.OtherKpiId equals kpi.Id
                where metric.Month == month && metric.Year == year
                select new { metric, kpi };

            if (!string.IsNullOrWhiteSpace(normalizedArea))
            {
                query = query.Where(x => x.metric.AreaCode.ToUpper() == normalizedArea);
            }

            var result = await query
                .OrderBy(x => x.kpi.Id)
                .Select(x => new OtherKpiMetricDto
                {
                    Id = x.metric.Id,
                    OtherKpiId = x.kpi.Id,
                    NetworkEngineerKpi = x.kpi.NetworkEngineerKpi,
                    Division = x.kpi.Division,
                    Section = x.kpi.Section,
                    KpiPercent = x.kpi.KpiPercent,
                    AreaCode = x.metric.AreaCode,
                    Site = x.metric.AreaCode,
                    KpiValue = x.metric.KpiValue,
                    Month = (byte)x.metric.Month,
                    Year = (short)x.metric.Year,
                    CreatedAt = x.metric.CreatedAt
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OtherKpiDto>> GetById(int id)
        {
            var item = await _db.OtherKpis
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new OtherKpiDto
                {
                    Id = x.Id,
                    NetworkEngineerKpi = x.NetworkEngineerKpi,
                    Division = x.Division,
                    Section = x.Section,
                    KpiPercent = x.KpiPercent
                })
                .FirstOrDefaultAsync();

            return item == null ? NotFound() : Ok(item);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<OtherKpiDto>> Create([FromBody] CreateOtherKpiDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            var entity = new OtherKpi
            {
                NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim(),
                Division = dto.Division,
                Section = dto.Section,
                KpiPercent = dto.KpiPercent
            };

            _db.OtherKpis.Add(entity);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new OtherKpiDto
            {
                Id = entity.Id,
                NetworkEngineerKpi = entity.NetworkEngineerKpi,
                Division = entity.Division,
                Section = entity.Section,
                KpiPercent = entity.KpiPercent
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateOtherKpiDto dto)
        {
            var entity = await _db.OtherKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            entity.NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim();
            entity.Division = dto.Division;
            entity.Section = dto.Section;
            entity.KpiPercent = dto.KpiPercent;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.OtherKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _db.OtherKpis.Remove(entity);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("metrics")]
        public async Task<ActionResult<OtherKpiMetricDto>> UpsertMetrics([FromBody] OtherKpiMetricDto dto)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            if (dto == null) return BadRequest("Request body is required.");
            if (dto.OtherKpiId <= 0) return BadRequest("OtherKpiId must be greater than zero.");
            if (dto.Month == 0 || dto.Year == 0) return BadRequest("Month and Year must be greater than zero.");

            var kpi = await _db.OtherKpis.FirstOrDefaultAsync(x => x.Id == dto.OtherKpiId);
            if (kpi == null) return NotFound($"Other KPI with id '{dto.OtherKpiId}' was not found.");

            var normalizedArea = NormalizeAreaCode(dto.AreaCode ?? dto.Site);
            if (string.IsNullOrWhiteSpace(normalizedArea)) return BadRequest("AreaCode is required.");

            var metric = await _db.OtherKpiMetrics.FirstOrDefaultAsync(x =>
                x.OtherKpiId == dto.OtherKpiId &&
                x.AreaCode.ToUpper() == normalizedArea &&
                x.Month == dto.Month &&
                x.Year == dto.Year);

            if (metric == null)
            {
                metric = new OtherKpiMetric
                {
                    OtherKpiId = dto.OtherKpiId,
                    AreaCode = normalizedArea,
                    KpiValue = dto.KpiValue,
                    Month = dto.Month,
                    Year = dto.Year,
                    CreatedAt = DateTime.UtcNow
                };
                _db.OtherKpiMetrics.Add(metric);
            }

            metric.AreaCode = normalizedArea;
            metric.KpiValue = dto.KpiValue;

            await _db.SaveChangesAsync();

            return Ok(ToMetricDto(metric, kpi));
        }

        [HttpDelete("metrics/{metricId:int}")]
        public async Task<IActionResult> DeleteMetric(int metricId)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            var row = await _db.OtherKpiMetrics.FirstOrDefaultAsync(x => x.Id == metricId);
            if (row == null) return NotFound();

            _db.OtherKpiMetrics.Remove(row);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static OtherKpiMetricDto ToMetricDto(OtherKpiMetric metric, OtherKpi kpi) => new()
        {
            Id = metric.Id,
            OtherKpiId = kpi.Id,
            NetworkEngineerKpi = kpi.NetworkEngineerKpi,
            Division = kpi.Division,
            Section = kpi.Section,
            KpiPercent = kpi.KpiPercent,
            AreaCode = metric.AreaCode,
            Site = metric.AreaCode,
            KpiValue = metric.KpiValue,
            Month = (byte)metric.Month,
            Year = (short)metric.Year,
            CreatedAt = metric.CreatedAt
        };

        private static string NormalizeAreaCode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
