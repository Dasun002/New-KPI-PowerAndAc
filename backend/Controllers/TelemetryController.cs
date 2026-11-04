using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TelemetryController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TelemetryController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? year, [FromQuery] int? month)
        {
            var q = _db.Telemetry.AsNoTracking();
            if (year.HasValue) q = q.Where(x => x.Year == year.Value);
            if (month.HasValue) q = q.Where(x => x.Month == month.Value);
            return Ok(await q.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _db.Telemetry.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("upsert")]
        public async Task<IActionResult> Upsert([FromBody] backend.DTOs.TelemetryDto dto)
        {
            var existing = await _db.Telemetry
                .FirstOrDefaultAsync(x => x.Designation == dto.Designation && x.Year == dto.Year && x.Month == dto.Month);

            if (existing != null)
            {
                existing.Percentage = dto.Percentage;
                existing.Node_Count = dto.Node_Count;
                _db.Telemetry.Update(existing);
            }
            else
            {
                var newItem = new Models.Telemetry
                {
                    Designation = dto.Designation,
                    Year = dto.Year,
                    Month = dto.Month,
                    Percentage = dto.Percentage,
                    Node_Count = dto.Node_Count
                };
                _db.Telemetry.Add(newItem);
            }

            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
