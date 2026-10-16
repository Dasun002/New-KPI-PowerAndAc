/*
 * File: EnterpriseKpiController.cs
 * CRUD endpoints for Enterprise KPI admin definitions.
 */

using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Helpers.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EnterpriseKpiController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationService _authorizationService;
        private const int PageId = 8;

        public EnterpriseKpiController(AppDbContext db, IAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }

        // GET all Enterprise KPI definitions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EnterpriseKpiDto>>> GetAll()
        {
            var items = await _db.EnterpriseKpis
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new EnterpriseKpiDto
                {
                    Id = x.Id,
                    NetworkEngineerKpi = x.NetworkEngineerKpi,
                    Division = x.Division,
                    KpiPercent = x.KpiPercent
                })
                .ToListAsync();

            if (items.Count >= 3)
            {
                var item1 = items[0];
                var item2 = items[1];
                var item3 = items[2];
                items[0] = item2;
                items[1] = item3;
                items[2] = item1;
            }

            return Ok(items);
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics([FromQuery] byte month, [FromQuery] short year, [FromQuery] string? site, [FromQuery] string? area)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            if (month == 0 || year == 0)
                return BadRequest("Month and Year must be greater than zero.");

            var normalizedSite = NormalizeSite(site ?? area);
            if (string.IsNullOrWhiteSpace(normalizedSite))
                return Ok(Array.Empty<object>());

            var rows = await (
                from metric in _db.EnterpriseKpiMetrics.AsNoTracking()
                join kpi in _db.EnterpriseKpis.AsNoTracking()
                    on metric.EnterpriseKpiId equals kpi.Id
                join et in _db.EnterpriseTargets.AsNoTracking()
                    on new { metric.EnterpriseKpiId, metric.Month, metric.Year }
                    equals new { et.EnterpriseKpiId, Month = (int)et.Month, Year = (int)et.Year }
                    into etJoin
                from et in etJoin.DefaultIfEmpty()
                where metric.Month == month && metric.Year == year
                select new { metric, kpi, et })
                .ToListAsync();

            rows = rows
                .Where(x => NormalizeSite(x.metric.Site) == normalizedSite)
                .OrderBy(x => x.kpi.Id)
                .ToList();

            var result = rows
                .Select(x => new
                {
                    id = x.kpi.Id,
                    networkEngineerKpi = x.kpi.NetworkEngineerKpi,
                    division = x.kpi.Division,
                    target = x.metric.Target != null ? x.metric.Target : (x.et != null ? x.et.Section : null),
                    kpiPercent = x.kpi.KpiPercent,
                    site = x.metric.Site ?? string.Empty,
                    kpi_value = x.metric.KpiValue,
                    month = x.metric.Month,
                    year = x.metric.Year
                })
                .ToList();

            if (result.Count >= 3)
            {
                var item1 = result[0];
                var item2 = result[1];
                var item3 = result[2];
                result[0] = item2;
                result[1] = item3;
                result[2] = item1;
            }

            return Ok(result);
        }

        // GET by ID
        [HttpGet("{id:int}")]
        public async Task<ActionResult<EnterpriseKpiDto>> GetById(int id)
        {
            var item = await _db.EnterpriseKpis
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new EnterpriseKpiDto
                {
                    Id = x.Id,
                    NetworkEngineerKpi = x.NetworkEngineerKpi,
                    Division = x.Division,
                    KpiPercent = x.KpiPercent
                })
                .FirstOrDefaultAsync();

            if (item == null) return NotFound();
            return Ok(item);
        }

        // POST create
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<EnterpriseKpiDto>> Create([FromBody] CreateEnterpriseKpiDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            var entity = new EnterpriseKpi
            {
                NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim(),
                Division = dto.Division,
                KpiPercent = dto.KpiPercent
            };

            _db.EnterpriseKpis.Add(entity);
            await _db.SaveChangesAsync();

            var result = new EnterpriseKpiDto
            {
                Id = entity.Id,
                NetworkEngineerKpi = entity.NetworkEngineerKpi,
                Division = entity.Division,
                KpiPercent = entity.KpiPercent
            };

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, result);
        }

        // PUT update
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateEnterpriseKpiDto dto)
        {
            var entity = await _db.EnterpriseKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            entity.NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim();
            entity.Division = dto.Division;
            entity.KpiPercent = dto.KpiPercent;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.EnterpriseKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _db.EnterpriseKpis.Remove(entity);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("metrics")]
        public async Task<IActionResult> UpsertMetrics([FromBody] EnterpriseKpiMetricDto dto)
        {
            try
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
                if (!authResult.Succeeded) return Forbid();

                if (dto == null) return BadRequest("Request body is required.");
                if (dto.EnterpriseKpiId <= 0) return BadRequest("EnterpriseKpiId must be > 0.");
                if (dto.Month == 0 || dto.Year == 0) return BadRequest("Month and Year must be greater than zero.");
                var kpi = await _db.EnterpriseKpis.FirstOrDefaultAsync(x => x.Id == dto.EnterpriseKpiId);
                if (kpi == null)
                    return NotFound($"Enterprise KPI with id '{dto.EnterpriseKpiId}' was not found.");

                var normalizedSite = NormalizeSite(dto.Site ?? dto.AreaCode);
                if (string.IsNullOrWhiteSpace(normalizedSite))
                    return BadRequest("Site is required.");

                var metricCandidates = await _db.EnterpriseKpiMetrics
                    .Where(x =>
                        x.EnterpriseKpiId == dto.EnterpriseKpiId &&
                        x.Month == dto.Month &&
                        x.Year == dto.Year)
                    .ToListAsync();

                var metric = metricCandidates.FirstOrDefault(x => NormalizeSite(x.Site) == normalizedSite);

                bool isNew = metric == null;
                if (isNew)
                {
                    metric = new EnterpriseKpiMetric
                    {
                        EnterpriseKpiId = dto.EnterpriseKpiId,
                        Site = normalizedSite,
                        KpiValue = dto.KpiValue,
                        Target = dto.Target,
                        Month = dto.Month,
                        Year = dto.Year,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.EnterpriseKpiMetrics.Add(metric);
                }
                else
                {
                    metric.Site = normalizedSite;
                    metric.KpiValue = dto.KpiValue;
                    metric.Target = dto.Target;
                    metric.Month = dto.Month;
                    metric.Year = dto.Year;
                }

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    id = kpi.Id,
                    networkEngineerKpi = kpi.NetworkEngineerKpi,
                    division = kpi.Division,
                    target = metric.Target,
                    kpiPercent = kpi.KpiPercent,
                    site = metric.Site,
                    kpi_value = metric.KpiValue,
                    month = metric.Month,
                    year = metric.Year,
                    isNew
                });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : "";
                return StatusCode(500, new { message = ex.Message, inner = innerMsg, details = ex.ToString() });
            }
        }

        [HttpDelete("metrics/{metricId:int}")]
        public async Task<IActionResult> DeleteMetric(int metricId)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            var row = await _db.EnterpriseKpiMetrics.FirstOrDefaultAsync(x => x.Id == metricId);
            if (row == null) return NotFound();

            _db.EnterpriseKpiMetrics.Remove(row);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        private static string NormalizeSite(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var filtered = new string(value.Where(char.IsLetterOrDigit).ToArray());
            return filtered.ToUpperInvariant();
        }
    }
}
