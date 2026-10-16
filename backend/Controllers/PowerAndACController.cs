using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PowerAndACController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationService _authorizationService;
        private const int PageId = 10; // OTHER_KPI (see Program.cs seeds)

        public PowerAndACController(AppDbContext db, IAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }


        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? year, [FromQuery] int? month)
        {
            var q = _db.PowerAndAC.AsNoTracking();
            if (year.HasValue) q = q.Where(x => x.Year == year.Value);
            if (month.HasValue) q = q.Where(x => x.Month == month.Value);

            var list = await q.OrderBy(x => x.Month).ThenBy(x => x.Designation).ToListAsync();

            var result = list.Select(p => new PowerAndACDto
            {
                Id = p.Id,
                Designation = p.Designation,
                Year = p.Year,
                Month = p.Month,
                Scheduled = p.Scheduled,
                Attended = p.Attended,
                Cumulative_Sched = p.Cumulative_Sched,
                Cumulative_Achieved = p.Cumulative_Achieved,
                IsVerified = p.IsVerified
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _db.PowerAndAC.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPatch("{id:int}/toggle-verified")]
        public async Task<IActionResult> ToggleVerified(int id)
        {
            var auth = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!auth.Succeeded) return Forbid();

            var entity = await _db.PowerAndAC.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.IsVerified })
                .FirstOrDefaultAsync();

            if (entity == null) return NotFound();

            var newValue = !entity.IsVerified;

            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE dbo.PowerAndAC SET [is_verified] = @p0 WHERE Id = @p1",
                newValue,
                id);

            return Ok(new { id = entity.Id, isVerified = newValue });
        }

    }
}
