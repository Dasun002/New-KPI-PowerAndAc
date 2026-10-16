/*
 * File: OtnOp2Controller.cs
 * Provides API endpoints for managing OTN OP KPI definitions and
 * associated site-level metrics, including CRUD operations and bulk metric upserts.
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
    // OTN OP2 KPI CONTROLLER
    // Handles KPI definitions and metrics for OTN OP section
    // =========================================================
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OtnOp2Controller : ControllerBase
    {
        // Database context for OTN OP2 data
        private readonly AppDbContext _db;

        // Authorization service for page-based permission checks
        private readonly IAuthorizationService _authorizationService;

        // Page identifier used in authorization policies
        private const int PageId = 4;

        // Inject dependencies
        public OtnOp2Controller(AppDbContext db, IAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }

        // =========================================================
        // GET ALL OTN OP2 KPI DEFINITIONS
        // Requires ViewPagePolicy authorization
        // =========================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OtnOp2Dto>>> GetAll()
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Retrieve KPI definitions ordered by ID
            var items = await _db.OtnOp2
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new OtnOp2Dto
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
        // GET OTN OP2 KPI BY ID
        // =========================================================
        [HttpGet("{id:int}")]
        public async Task<ActionResult<OtnOp2Dto>> GetById(int id)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Retrieve KPI definition by ID
            var item = await _db.OtnOp2
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new OtnOp2Dto
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
        // CREATE NEW OTN OP2 KPI
        // Admin-only endpoint
        // =========================================================
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<OtnOp2Dto>> Create([FromBody] CreateOtnOp2Dto dto)
        {
            // Validate KPI name
            if (string.IsNullOrWhiteSpace(dto.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            // Create entity from DTO
            var entity = new OtnOp2
            {
                NetworkEngineerKpi = dto.NetworkEngineerKpi.Trim(),
                Division = dto.Division,
                Section = dto.Section,
                KpiPercent = dto.KpiPercent
            };

            _db.OtnOp2.Add(entity);
            await _db.SaveChangesAsync();

            // Map entity to DTO for response
            var result = new OtnOp2Dto
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
        // UPDATE EXISTING OTN OP2 KPI
        // Admin-only endpoint
        // =========================================================
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateOtnOp2Dto dto)
        {
            var entity = await _db.OtnOp2.FirstOrDefaultAsync(x => x.Id == id);
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
        // DELETE OTN OP2 KPI
        // Admin-only endpoint
        // =========================================================
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.OtnOp2.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _db.OtnOp2.Remove(entity);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // =========================================================
        // GET KPI METRICS FOR SPECIFIC MONTH/YEAR
        // =========================================================
        [HttpGet("{id:int}/metrics")]
        public async Task<ActionResult<IEnumerable<OtnOp2MetricDto>>> GetMetrics(
            int id,
            [FromQuery] short year,
            [FromQuery] byte month)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Ensure KPI definition exists
            var exists = await _db.OtnOp2.AsNoTracking().AnyAsync(x => x.Id == id);
            if (!exists) return NotFound("OtnOp2 KPI not found. Use the KPI id from /api/OtnOp2, not a metric id.");

            // Retrieve metrics for the specified month and year
            var rows = await _db.OtnOp2Metrics
                .AsNoTracking()
                .Where(m => m.OtnOp2Id == id && m.Year == year && m.Month == month)
                .OrderBy(m => m.Site)
                .Select(m => new OtnOp2MetricDto
                {
                    Id = m.Id,
                    OtnOp2Id = m.OtnOp2Id,
                    Site = m.Site,
                    TotalFailedLinks = m.TotalFailedLinks,
                    LinksSlaNotViolated = m.LinksSlaNotViolated,
                    Year = m.Year,
                    Month = m.Month
                })
                .ToListAsync();

            return Ok(rows);
        }

        // =========================================================
        // BULK UPSERT KPI METRICS
        // Inserts new metrics or updates existing ones based on
        // unique key (OtnOp2Id, Site, Year, Month)
        // =========================================================
        [HttpPost("{id:int}/metrics")]
        public async Task<IActionResult> UpsertMetrics(int id, [FromBody] List<OtnOp2MetricDto> metrics)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            // Ensure KPI definition exists
            var exists = await _db.OtnOp2.AnyAsync(x => x.Id == id);
            if (!exists) return NotFound("OtnOp2 KPI not found. Use the KPI id from /api/OtnOp2, not a metric id.");

            if (metrics == null || metrics.Count == 0)
                return BadRequest("Metrics list is empty.");

            foreach (var dto in metrics)
            {
                // Validate site
                if (string.IsNullOrWhiteSpace(dto.Site))
                    return BadRequest("Site is required.");

                // Validate month
                if (dto.Month < 1 || dto.Month > 12)
                    return BadRequest("Month must be 1..12.");

                // Force FK from route
                dto.OtnOp2Id = id;

                var site = dto.Site.Trim();

                // Check if metric already exists
                var existing = await _db.OtnOp2Metrics
                    .FirstOrDefaultAsync(m =>
                        m.OtnOp2Id == id &&
                        m.Site == site &&
                        m.Year == dto.Year &&
                        m.Month == dto.Month);

                if (existing == null)
                {
                    // Insert new metric
                    _db.OtnOp2Metrics.Add(new OtnOp2Metrics
                    {
                        OtnOp2Id = id,
                        Site = site,
                        TotalFailedLinks = dto.TotalFailedLinks,
                        LinksSlaNotViolated = dto.LinksSlaNotViolated,
                        Year = dto.Year,
                        Month = dto.Month
                    });
                }
                else
                {
                    // Update existing metric
                    existing.TotalFailedLinks = dto.TotalFailedLinks;
                    existing.LinksSlaNotViolated = dto.LinksSlaNotViolated;
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
            // Require edit permission
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            var row = await _db.OtnOp2Metrics.FirstOrDefaultAsync(x => x.Id == metricId);
            if (row == null) return NotFound();

            _db.OtnOp2Metrics.Remove(row);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}