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
    public class OtherOperatorKpiController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationService _authorizationService;
        private const int PageId = 9;

        public OtherOperatorKpiController(AppDbContext db, IAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OtherOperatorKpiDto>>> GetAll()
        {
            var items = await _db.OtherOperatorKpis
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new OtherOperatorKpiDto
                {
                    Id = x.Id,
                    NetworkEngineerKpi = x.NetworkEngineerKpi,
                    Division = x.Division,
                    KpiPercent = x.KpiPercent
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OtherOperatorKpiDto>> GetById(int id)
        {
            var item = await _db.OtherOperatorKpis
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new OtherOperatorKpiDto
                {
                    Id = x.Id,
                    NetworkEngineerKpi = x.NetworkEngineerKpi,
                    Division = x.Division,
                    KpiPercent = x.KpiPercent
                })
                .FirstOrDefaultAsync();

            return item == null ? NotFound() : Ok(item);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<OtherOperatorKpiDto>> Create([FromBody] CreateOtherOperatorKpiDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            var entity = new OtherOperatorKpi
            {
                NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim(),
                Division = dto.Division
            };

            _db.OtherOperatorKpis.Add(entity);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new OtherOperatorKpiDto
            {
                Id = entity.Id,
                NetworkEngineerKpi = entity.NetworkEngineerKpi,
                Division = entity.Division
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateOtherOperatorKpiDto dto)
        {
            var entity = await _db.OtherOperatorKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            entity.NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim();
            entity.Division = dto.Division;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.OtherOperatorKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _db.OtherOperatorKpis.Remove(entity);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("metrics")]
        public async Task<ActionResult<IEnumerable<OtherOperatorKpiMetricDto>>> GetMetrics(
            [FromQuery] byte month,
            [FromQuery] short year,
            [FromQuery] string? site)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();
            if (month == 0 || year == 0) return BadRequest("Month and Year must be greater than zero.");

            var query =
                from metric in _db.OtherOperatorKpiMetrics.AsNoTracking()
                join kpi in _db.OtherOperatorKpis.AsNoTracking()
                    on metric.OtherOperatorKpiId equals kpi.Id
                join target in _db.OtherOperatorTargets.AsNoTracking()
                    on new { metric.OtherOperatorKpiId, metric.Month, metric.Year }
                    equals new { target.OtherOperatorKpiId, target.Month, target.Year }
                    into targetJoin
                from target in targetJoin.DefaultIfEmpty()
                where metric.Month == month && metric.Year == year
                select new { metric, kpi, target };

            if (!string.IsNullOrWhiteSpace(site))
            {
                var normalized = NormalizeSite(site);
                query = query.Where(x => x.metric.Site != null && x.metric.Site.ToUpper() == normalized);
            }

            var result = await query
                .OrderBy(x => x.kpi.Id)
                .Select(x => new OtherOperatorKpiMetricDto
                {
                    Id = x.metric.Id,
                    OtherOperatorKpiId = x.kpi.Id,
                    NetworkEngineerKpi = x.kpi.NetworkEngineerKpi,
                    Division = x.kpi.Division,
                    Site = x.metric.Site ?? string.Empty,
                    KpiValue = x.metric.KpiValue,
                    Target = x.metric.Target != null ? x.metric.Target : (x.target != null ? x.target.Section : null),
                    KpiPercent = x.kpi.KpiPercent,
                    Month = x.metric.Month,
                    Year = x.metric.Year
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpPost("metrics")]
        public async Task<ActionResult<OtherOperatorKpiMetricDto>> UpsertMetrics([FromBody] OtherOperatorKpiMetricDto dto)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            if (dto == null) return BadRequest("Request body is required.");
            if (dto.OtherOperatorKpiId <= 0) return BadRequest("OtherOperatorKpiId must be greater than zero.");
            if (dto.Month == 0 || dto.Year == 0) return BadRequest("Month and Year must be greater than zero.");

            var kpi = await _db.OtherOperatorKpis.FirstOrDefaultAsync(x => x.Id == dto.OtherOperatorKpiId);
            if (kpi == null) return NotFound($"Other Operator KPI with id '{dto.OtherOperatorKpiId}' was not found.");

            var normalizedSite = NormalizeSite(dto.Site);
            if (string.IsNullOrWhiteSpace(normalizedSite)) return BadRequest("Site is required.");

            var metric = await _db.OtherOperatorKpiMetrics.FirstOrDefaultAsync(x =>
                x.OtherOperatorKpiId == dto.OtherOperatorKpiId &&
                x.Site != null &&
                x.Site.ToUpper() == normalizedSite &&
                x.Month == dto.Month &&
                x.Year == dto.Year);

            if (metric == null)
            {
                metric = new OtherOperatorKpiMetric
                {
                    OtherOperatorKpiId = dto.OtherOperatorKpiId,
                    Site = normalizedSite,
                    Month = dto.Month,
                    Year = dto.Year
                };
                _db.OtherOperatorKpiMetrics.Add(metric);
            }

            metric.KpiValue = dto.KpiValue;
            metric.Target = dto.Target;

            await _db.SaveChangesAsync();

            return Ok(new OtherOperatorKpiMetricDto
            {
                Id = metric.Id,
                OtherOperatorKpiId = kpi.Id,
                NetworkEngineerKpi = kpi.NetworkEngineerKpi,
                Division = kpi.Division,
                Site = metric.Site ?? string.Empty,
                KpiValue = metric.KpiValue,
                Target = metric.Target,
                KpiPercent = kpi.KpiPercent,
                Month = metric.Month,
                Year = metric.Year
            });
        }

        [HttpDelete("metrics/{metricId:int}")]
        public async Task<IActionResult> DeleteMetric(int metricId)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            var row = await _db.OtherOperatorKpiMetrics.FirstOrDefaultAsync(x => x.Id == metricId);
            if (row == null) return NotFound();

            _db.OtherOperatorKpiMetrics.Remove(row);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static string NormalizeSite(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
