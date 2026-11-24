using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/enterprise-targets")]
    [Authorize]
    public class EnterpriseTargetsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public EnterpriseTargetsController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EnterpriseTargetDto>>> GetAll()
        {
            var items = await _db.EnterpriseTargets
                .AsNoTracking()
                .Join(_db.EnterpriseKpis.AsNoTracking(),
                    t => t.EnterpriseKpiId,
                    k => k.Id,
                    (t, k) => new EnterpriseTargetDto
                    {
                        Id = t.Id,
                        EnterpriseKpiId = t.EnterpriseKpiId,
                        NetworkEngineerKpi = k.NetworkEngineerKpi,
                        Division = k.Division,
                        Section = t.Section,
                        Month = t.Month,
                        Year = t.Year
                    })
                .OrderBy(x => x.EnterpriseKpiId)
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<EnterpriseTargetDto>> GetById(int id)
        {
            var item = await _db.EnterpriseTargets
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Join(_db.EnterpriseKpis.AsNoTracking(),
                    t => t.EnterpriseKpiId,
                    k => k.Id,
                    (t, k) => new EnterpriseTargetDto
                    {
                        Id = t.Id,
                        EnterpriseKpiId = t.EnterpriseKpiId,
                        NetworkEngineerKpi = k.NetworkEngineerKpi,
                        Division = k.Division,
                        Section = t.Section,
                        Month = t.Month,
                        Year = t.Year
                    })
                .FirstOrDefaultAsync();

            return item == null ? NotFound() : Ok(item);
        }

        [HttpGet("by-kpi/{kpiId:int}")]
        public async Task<ActionResult<IEnumerable<EnterpriseTargetDto>>> GetByKpiId(int kpiId)
        {
            var kpi = await _db.EnterpriseKpis.AsNoTracking().FirstOrDefaultAsync(x => x.Id == kpiId);
            if (kpi == null) return NotFound();

            var items = await _db.EnterpriseTargets
                .AsNoTracking()
                .Where(t => t.EnterpriseKpiId == kpiId)
                .Select(t => new EnterpriseTargetDto
                {
                    Id = t.Id,
                    EnterpriseKpiId = t.EnterpriseKpiId,
                    NetworkEngineerKpi = kpi.NetworkEngineerKpi,
                    Division = kpi.Division,
                    Section = t.Section,
                    Month = t.Month,
                    Year = t.Year
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<EnterpriseTargetDto>> Create([FromBody] CreateEnterpriseTargetDto dto)
        {
            if (dto.EnterpriseKpiId <= 0) return BadRequest("EnterpriseKpiId is required.");
            if (dto.Month == 0 || dto.Year == 0) return BadRequest("Month and Year must be greater than zero.");

            var kpi = await _db.EnterpriseKpis.FirstOrDefaultAsync(x => x.Id == dto.EnterpriseKpiId);
            if (kpi == null) return NotFound($"EnterpriseKpi with id '{dto.EnterpriseKpiId}' not found.");

            var entity = new EnterpriseTarget
            {
                EnterpriseKpiId = dto.EnterpriseKpiId,
                Section = dto.Section,
                Month = dto.Month,
                Year = dto.Year
            };

            _db.EnterpriseTargets.Add(entity);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new EnterpriseTargetDto
            {
                Id = entity.Id,
                EnterpriseKpiId = entity.EnterpriseKpiId,
                NetworkEngineerKpi = kpi.NetworkEngineerKpi,
                Division = kpi.Division,
                Section = entity.Section,
                Month = entity.Month,
                Year = entity.Year
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateEnterpriseTargetDto dto)
        {
            var entity = await _db.EnterpriseTargets.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            if (dto.Month == 0 || dto.Year == 0) return BadRequest("Month and Year must be greater than zero.");

            if (dto.EnterpriseKpiId > 0)
                entity.EnterpriseKpiId = dto.EnterpriseKpiId;

            entity.Section = dto.Section;
            entity.Month = dto.Month;
            entity.Year = dto.Year;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.EnterpriseTargets.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _db.EnterpriseTargets.Remove(entity);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
