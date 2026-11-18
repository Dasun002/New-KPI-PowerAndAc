using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/other-operator-targets")]
    [Authorize]
    public class OtherOperatorTargetsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public OtherOperatorTargetsController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OtherOperatorTargetDto>>> GetAll()
        {
            var items = await _db.OtherOperatorTargets
                .AsNoTracking()
                .Join(_db.OtherOperatorKpis.AsNoTracking(),
                    t => t.OtherOperatorKpiId,
                    k => k.Id,
                    (t, k) => new OtherOperatorTargetDto
                    {
                        Id = t.Id,
                        OtherOperatorKpiId = t.OtherOperatorKpiId,
                        NetworkEngineerKpi = k.NetworkEngineerKpi,
                        Division = k.Division,
                        Section = t.Section,
                        Month = t.Month,
                        Year = t.Year
                    })
                .OrderBy(x => x.OtherOperatorKpiId)
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OtherOperatorTargetDto>> GetById(int id)
        {
            var item = await _db.OtherOperatorTargets
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Join(_db.OtherOperatorKpis.AsNoTracking(),
                    t => t.OtherOperatorKpiId,
                    k => k.Id,
                    (t, k) => new OtherOperatorTargetDto
                    {
                        Id = t.Id,
                        OtherOperatorKpiId = t.OtherOperatorKpiId,
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
        public async Task<ActionResult<IEnumerable<OtherOperatorTargetDto>>> GetByKpiId(int kpiId)
        {
            var kpi = await _db.OtherOperatorKpis.AsNoTracking().FirstOrDefaultAsync(x => x.Id == kpiId);
            if (kpi == null) return NotFound();

            var items = await _db.OtherOperatorTargets
                .AsNoTracking()
                .Where(t => t.OtherOperatorKpiId == kpiId)
                .Select(t => new OtherOperatorTargetDto
                {
                    Id = t.Id,
                    OtherOperatorKpiId = t.OtherOperatorKpiId,
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
        public async Task<ActionResult<OtherOperatorTargetDto>> Create([FromBody] CreateOtherOperatorTargetDto dto)
        {
            if (dto.OtherOperatorKpiId <= 0) return BadRequest("OtherOperatorKpiId is required.");
            if (dto.Month == 0 || dto.Year == 0) return BadRequest("Month and Year must be greater than zero.");

            var kpi = await _db.OtherOperatorKpis.FirstOrDefaultAsync(x => x.Id == dto.OtherOperatorKpiId);
            if (kpi == null) return NotFound($"OtherOperatorKpi with id '{dto.OtherOperatorKpiId}' not found.");

            var entity = new OtherOperatorTarget
            {
                OtherOperatorKpiId = dto.OtherOperatorKpiId,
                Section = dto.Section,
                Month = dto.Month,
                Year = dto.Year
            };

            _db.OtherOperatorTargets.Add(entity);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new OtherOperatorTargetDto
            {
                Id = entity.Id,
                OtherOperatorKpiId = entity.OtherOperatorKpiId,
                NetworkEngineerKpi = kpi.NetworkEngineerKpi,
                Division = kpi.Division,
                Section = entity.Section,
                Month = entity.Month,
                Year = entity.Year
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateOtherOperatorTargetDto dto)
        {
            var entity = await _db.OtherOperatorTargets.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            if (dto.Month == 0 || dto.Year == 0) return BadRequest("Month and Year must be greater than zero.");

            if (dto.OtherOperatorKpiId > 0)
                entity.OtherOperatorKpiId = dto.OtherOperatorKpiId;

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
            var entity = await _db.OtherOperatorTargets.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _db.OtherOperatorTargets.Remove(entity);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
