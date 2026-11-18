/*
 * File: ServiceFulfilmentKpiController.cs
 * Provides API endpoints for managing Service Fulfilment KPIs and their
 * associated area-level KPI metrics including CRUD operations and metric upserts.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
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
    // SERVICE FULFILMENT KPI CONTROLLER
    // Handles KPI definitions and KPI metric values per area
    // =========================================================
    [ApiController]
    [Route("api/service-fulfilment-kpi")]
    
    [Authorize]
    public class ServiceFulfilmentKpiController : ControllerBase
    {
        // Database context
        private readonly AppDbContext _context;

        // Authorization service for page-based permission checks
        private readonly IAuthorizationService _authorizationService;

        // Page identifier used for authorization
        private const int PageId = 2;

        // Inject dependencies
        public ServiceFulfilmentKpiController(AppDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        // =========================================================
        // MASTER KPI LIST
        // GET /service-fulfilment-kpi?month=11&year=2025
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] byte? month, [FromQuery] short? year)
        {
            // Check view permission
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            var query = _context.ServiceFulfilmentKpis.AsNoTracking();

            // Apply month filter if provided
            if (month.HasValue && month.Value > 0)
                query = query.Where(x => x.Month == month.Value);

            // Apply year filter if provided
            if (year.HasValue && year.Value > 0)
                query = query.Where(x => x.Year == year.Value);

            // Retrieve KPI definitions
            var result = await query
                .OrderBy(x => x.Id)
                .Select(x => new ServiceFulfilmentKpiDto
                {
                    Id = x.Id,
                    Kpi = x.Kpi,
                    Target = x.Target,
                    Calculation = x.Calculation,
                    Platform = x.Platform,
                    ResponsibleDgm = x.ResponsibleDgm,
                    DefineDoladetails = x.DefineDoladetails,
                    Weightage = x.Weightage,
                    DataSources = x.DataSources,
                    Month = x.Month,
                    Year = x.Year,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();

            return Ok(result);
        }

        // =========================================================
        // GET KPI BY ID
        // =========================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            // Check view permission
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            var entity = await _context.ServiceFulfilmentKpis
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null) return NotFound();

            return Ok(MapToDto(entity));
        }

        // =========================================================
        // CREATE NEW KPI
        // POST /service-fulfilment-kpi/add
        // =========================================================
        [HttpPost("add")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Add([FromBody] ServiceFulfilmentKpiDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var now = DateTime.UtcNow;

            // Create new KPI entity
            var entity = new ServiceFulfilmentKpi
            {
                // Identity column is auto-generated
                UpdatedAt = now.ToString("O"),
                Month = dto.Month != 0 ? dto.Month : (byte)now.Month,
                Year = dto.Year != 0 ? dto.Year : (short)now.Year
            };

            // Apply DTO values
            ApplyDtoToEntity(entity, dto);

            _context.ServiceFulfilmentKpis.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
        }

        // =========================================================
        // UPDATE EXISTING KPI
        // PUT /service-fulfilment-kpi/update/{id}
        // =========================================================
        [HttpPut("update/{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] ServiceFulfilmentKpiDto dto)
        {
            var entity = await _context.ServiceFulfilmentKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            // Apply updated values
            ApplyDtoToEntity(entity, dto);

            // Update modification timestamp
            entity.UpdatedAt = DateTime.UtcNow.ToString("O");

            await _context.SaveChangesAsync();

            return Ok(MapToDto(entity));
        }

        // =========================================================
        // DELETE KPI
        // =========================================================
        [HttpDelete("delete/{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.ServiceFulfilmentKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _context.ServiceFulfilmentKpis.Remove(entity);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =========================================================
        // KPI METRICS
        // GET /service-fulfilment-kpi/metrics?month=11&year=2025&area=CENHKMD
        // =========================================================
        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics([FromQuery] byte month, [FromQuery] short year, [FromQuery] string? area)
        {
            // Check view permission
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            if (month == 0 || year == 0)
                return BadRequest("Month and Year must be greater than zero.");

            // Join KPI definitions with KPI metric values
            var query =
                from metric in _context.ServiceFulfilmentKpiMetrics.AsNoTracking()
                join kpi in _context.ServiceFulfilmentKpis.AsNoTracking()
                    on metric.ServiceFulfilmentKpiId equals kpi.Id
                where metric.Month == month && metric.Year == year
                select new { metric, kpi };

            // Filter by area if provided
            if (!string.IsNullOrWhiteSpace(area))
            {
                var normalized = area.Trim().ToUpper();
                query = query.Where(x => x.metric.AreaCode != null && x.metric.AreaCode.ToUpper() == normalized);
            }

            var result = await query
                .OrderBy(x => x.kpi.Id)
                .Select(x => new
                {
                    id = x.kpi.Id,
                    kpi = x.kpi.Kpi,
                    target = x.kpi.Target,
                    platform = x.kpi.Platform,
                    responsibleDgm = x.kpi.ResponsibleDgm,
                    definedoladetails = x.kpi.DefineDoladetails,
                    weightage = x.kpi.Weightage,
                    area = x.metric.AreaCode ?? string.Empty,
                    kpi_value = x.metric.KpiValue,
                    month = x.metric.Month,
                    year = x.metric.Year
                })
                .ToListAsync();

            return Ok(result);
        }

        // =========================================================
        // UPSERT KPI METRIC VALUE
        // =========================================================
        [HttpPost("metrics")]
        public async Task<IActionResult> UpsertMetric([FromBody] UpsertServiceFulfilmentMetricDto dto)
        {
            // Platform-level edit permission
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            if (dto == null) return BadRequest("Request body is required.");
            if (dto.ServiceFulfilmentKpiId <= 0) return BadRequest("ServiceFulfilmentKpiId must be > 0.");
            if (dto.Month == 0 || dto.Year == 0) return BadRequest("Month and Year must be greater than zero.");

            // Ensure KPI exists
            var kpi = await _context.ServiceFulfilmentKpis
                .FirstOrDefaultAsync(x => x.Id == dto.ServiceFulfilmentKpiId);

            if (kpi == null)
                return NotFound($"Service Fulfilment KPI with id '{dto.ServiceFulfilmentKpiId}' was not found.");

            // Normalize area code
            var normalizedArea = (dto.AreaCode ?? string.Empty).Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(normalizedArea))
                return BadRequest("AreaCode is required.");

            // Check if metric already exists
            var metric = await _context.ServiceFulfilmentKpiMetrics.FirstOrDefaultAsync(x =>
                x.ServiceFulfilmentKpiId == dto.ServiceFulfilmentKpiId &&
                x.AreaCode.ToUpper() == normalizedArea &&
                x.Month == dto.Month &&
                x.Year == dto.Year);

            if (metric == null)
            {
                // Insert new metric record
                metric = new ServiceFulfilmentKpiMetric
                {
                    ServiceFulfilmentKpiId = dto.ServiceFulfilmentKpiId,
                    AreaCode = normalizedArea,
                    KpiValue = dto.KpiValue,
                    Month = dto.Month,
                    Year = dto.Year
                };

                _context.ServiceFulfilmentKpiMetrics.Add(metric);
            }
            else
            {
                // Update existing metric
                metric.AreaCode = normalizedArea;
                metric.KpiValue = dto.KpiValue;
                metric.Month = dto.Month;
                metric.Year = dto.Year;
            }

            await _context.SaveChangesAsync();

            // Build response payload
            var response = new
            {
                id = kpi.Id,
                kpi = kpi.Kpi,
                target = kpi.Target,
                platform = kpi.Platform,
                responsibleDgm = kpi.ResponsibleDgm,
                definedoladetails = kpi.DefineDoladetails,
                weightage = kpi.Weightage,
                area = metric.AreaCode,
                kpi_value = metric.KpiValue,
                month = metric.Month,
                year = metric.Year
            };

            return Ok(response);
        }

        // =========================================================
        // ENTITY → DTO MAPPING
        // =========================================================
        private static ServiceFulfilmentKpiDto MapToDto(ServiceFulfilmentKpi entity) => new ServiceFulfilmentKpiDto
        {
            Id = entity.Id,
            Kpi = entity.Kpi,
            Target = entity.Target,
            Calculation = entity.Calculation,
            Platform = entity.Platform,
            ResponsibleDgm = entity.ResponsibleDgm,
            DefineDoladetails = entity.DefineDoladetails,
            Weightage = entity.Weightage,
            DataSources = entity.DataSources,
            Month = entity.Month,
            Year = entity.Year,
            UpdatedAt = entity.UpdatedAt
        };

        // =========================================================
        // APPLY DTO VALUES TO ENTITY
        // =========================================================
        private static void ApplyDtoToEntity(ServiceFulfilmentKpi entity, ServiceFulfilmentKpiDto dto)
        {
            entity.Kpi = dto.Kpi ?? string.Empty;
            entity.Target = dto.Target ?? string.Empty;
            entity.Calculation = dto.Calculation ?? string.Empty;
            entity.Platform = dto.Platform ?? string.Empty;
            entity.ResponsibleDgm = dto.ResponsibleDgm ?? string.Empty;
            entity.DefineDoladetails = dto.DefineDoladetails ?? string.Empty;
            entity.Weightage = dto.Weightage;
            entity.DataSources = dto.DataSources ?? string.Empty;

            if (dto.Month > 0) entity.Month = dto.Month;
            if (dto.Year > 0) entity.Year = dto.Year;

            if (!string.IsNullOrWhiteSpace(dto.UpdatedAt))
                entity.UpdatedAt = dto.UpdatedAt!;
        }
    }
}