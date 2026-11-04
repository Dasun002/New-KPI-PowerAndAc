/*
 * File: OtnOp1Controller.cs
 * Provides API endpoints for managing OTN OP KPIs and their associated
 * monthly site metrics, including CRUD operations and bulk metric upserts.
 */

using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;
using backend.Helpers.Authorization;

namespace backend.Controllers
{
    // =========================================================
    // OTN OP KPI CONTROLLER
    // Handles KPI definitions and site-level metrics for OTN OP
    // =========================================================
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OtnOp1Controller : ControllerBase
    {
        // Database context for OTN OP data
        private readonly AppDbContext _db;

        // Authorization service for page-based policies
        private readonly IAuthorizationService _authorizationService;

        // Page identifier used for authorization checks
        private const int PageId = 4;

        // Inject dependencies
        public OtnOp1Controller(AppDbContext db, IAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }

        // =========================================================
        // GET ALL OTN OP KPIs
        // Requires ViewPagePolicy authorization
        // =========================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OtnOp1Dto>>> GetAll()
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Retrieve KPI definitions ordered by ID
            var items = await _db.OtnOp1
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new OtnOp1Dto
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

        // =========================================================
        // GET OTN OP KPI BY ID
        // =========================================================
        [HttpGet("{id:int}")]
        public async Task<ActionResult<OtnOp1Dto>> GetById(int id)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Retrieve KPI definition by ID
            var item = await _db.OtnOp1
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new OtnOp1Dto
                {
                    Id = x.Id,
                    NetworkEngineerKpi = x.NetworkEngineerKpi,
                    Division = x.Division,
                    Section = x.Section,
                    KpiPercent = x.KpiPercent
                })
                .FirstOrDefaultAsync();

            if (item == null) return NotFound();

            return Ok(item);
        }

        // =========================================================
        // CREATE NEW OTN OP KPI
        // Admin-only endpoint
        // =========================================================
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<OtnOp1Dto>> Create([FromBody] CreateOtnOp1Dto dto)
        {
            // Validate KPI name
            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            // Create entity from DTO
            var entity = new OtnOp1
            {
                NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim(),
                Division = dto.Division,
                Section = dto.Section,
                KpiPercent = dto.KpiPercent
            };

            _db.OtnOp1.Add(entity);
            await _db.SaveChangesAsync();

            // Map entity to DTO for response
            var result = new OtnOp1Dto
            {
                Id = entity.Id,
                NetworkEngineerKpi = entity.NetworkEngineerKpi,
                Division = entity.Division,
                Section = entity.Section,
                KpiPercent = entity.KpiPercent
            };

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, result);
        }

        // =========================================================
        // UPDATE EXISTING OTN OP KPI
        // Admin-only endpoint
        // =========================================================
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateOtnOp1Dto dto)
        {
            var entity = await _db.OtnOp1.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            // Validate KPI name
            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            // Update fields
            entity.NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim();
            entity.Division = dto.Division;
            entity.Section = dto.Section;
            entity.KpiPercent = dto.KpiPercent;

            await _db.SaveChangesAsync();

            return NoContent();
        }

        // =========================================================
        // DELETE OTN OP KPI
        // Cascade delete will remove associated metrics
        // =========================================================
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.OtnOp1.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _db.OtnOp1.Remove(entity);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // =========================================================
        // GET KPI METRICS FOR SPECIFIC MONTH/YEAR
        // =========================================================
        [HttpGet("{id:int}/metrics")]
        public async Task<ActionResult<IEnumerable<OtnOp1MetricDto>>> GetMetrics(
            int id,
            [FromQuery] short year,
            [FromQuery] byte month)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Ensure KPI definition exists
            var exists = await _db.OtnOp1.AsNoTracking().AnyAsync(x => x.Id == id);
            if (!exists) return NotFound("OtnOp1 KPI not found. Use the KPI id from /api/OtnOp1, not a metric id.");

            // Retrieve metrics for the specified month and year
            var rows = await _db.OtnOp1Metrics
                .AsNoTracking()
                .Where(m => m.OtnOp1Id == id && m.Year == year && m.Month == month)
                .OrderBy(m => m.Site)
                .Select(m => new OtnOp1MetricDto
                {
                    Id = m.Id,
                    OtnOp1Id = m.OtnOp1Id,
                    Site = m.Site,
                    UnavailableMinutes = m.UnavailableMinutes,
                    TotalMinutes = m.TotalMinutes,
                    TotalNodes = m.TotalNodes,
                    Year = m.Year,
                    Month = m.Month
                })
                .ToListAsync();

            return Ok(rows);
        }

        // =========================================================
        // BULK UPSERT KPI METRICS
        // Inserts new metrics or updates existing ones based on
        // unique key (OtnOp1Id, Site, Year, Month)
        // =========================================================
        [HttpPost("{id:int}/metrics")]
        public async Task<IActionResult> UpsertMetrics(int id, [FromBody] List<OtnOp1MetricDto> metrics)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            // Ensure KPI definition exists
            var exists = await _db.OtnOp1.AnyAsync(x => x.Id == id);
            if (!exists) return NotFound("OtnOp1 KPI not found. Use the KPI id from /api/OtnOp1, not a metric id.");

            if (metrics == null || metrics.Count == 0)
                return BadRequest("Metrics list is empty.");

            foreach (var dto in metrics)
            {
                // Validate site name
                if (string.IsNullOrWhiteSpace(dto.Site))
                    return BadRequest("Site is required.");

                // Validate month range
                if (dto.Month < 1 || dto.Month > 12)
                    return BadRequest("Month must be 1..12.");

                // Force foreign key from route
                dto.OtnOp1Id = id;

                var site = dto.Site.Trim();

                // Check if metric already exists for this site/month/year
                var existing = await _db.OtnOp1Metrics
                    .FirstOrDefaultAsync(m =>
                        m.OtnOp1Id == id &&
                        m.Site == site &&
                        m.Year == dto.Year &&
                        m.Month == dto.Month);

                if (existing == null)
                {
                    // Insert new metric record
                    _db.OtnOp1Metrics.Add(new OtnOp1Metrics
                    {
                        OtnOp1Id = id,
                        Site = site,
                        UnavailableMinutes = dto.UnavailableMinutes,
                        TotalMinutes = dto.TotalMinutes,
                        TotalNodes = dto.TotalNodes,
                        Year = dto.Year,
                        Month = dto.Month
                    });
                }
                else
                {
                    // Update existing metric record
                    existing.UnavailableMinutes = dto.UnavailableMinutes;
                    existing.TotalMinutes = dto.TotalMinutes;
                    existing.TotalNodes = dto.TotalNodes;
                }
            }

            await _db.SaveChangesAsync();

            return Ok();
        }

        // =========================================================
        // DELETE SPECIFIC KPI METRIC
        // =========================================================
        [HttpDelete("metrics/{metricId:int}")]
        public async Task<IActionResult> DeleteMetric(int metricId)
        {
            // Require edit permission for this platform KPI
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            var row = await _db.OtnOp1Metrics.FirstOrDefaultAsync(x => x.Id == metricId);
            if (row == null) return NotFound();

            _db.OtnOp1Metrics.Remove(row);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}