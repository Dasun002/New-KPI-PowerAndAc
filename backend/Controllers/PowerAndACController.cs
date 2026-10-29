using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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
        private readonly ILogger<PowerAndACController> _logger;
        private const int PageId = 11; // POWER_AC (see Program.cs seeds)

        public PowerAndACController(
            AppDbContext db,
            IAuthorizationService authorizationService,
            ILogger<PowerAndACController> logger)
        {
            _db = db;
            _authorizationService = authorizationService;
            _logger = logger;
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

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create([FromBody] PowerAndACWriteDto request)
        {
            var designation = request.Designation.Trim();
            if (designation.Length == 0)
                return BadRequest(new { message = "Designation is required." });

            var duplicate = await _db.PowerAndAC.AnyAsync(x =>
                x.Designation == designation && x.Year == request.Year && x.Month == request.Month);
            if (duplicate)
                return Conflict(new { message = "A record already exists for this designation, year, and month." });

            var entity = new PowerAndAC
            {
                Designation = designation,
                Year = request.Year,
                Month = request.Month,
                Scheduled = request.Scheduled,
                Attended = request.Attended,
                Cumulative_Sched = request.Cumulative_Sched,
                Cumulative_Achieved = request.Cumulative_Achieved
            };

            _db.PowerAndAC.Add(entity);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return HandleDatabaseWriteError(ex, "create");
            }

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] PowerAndACWriteDto request)
        {
            var entity = await _db.PowerAndAC.FindAsync(id);
            if (entity == null) return NotFound();

            var designation = request.Designation.Trim();
            if (designation.Length == 0)
                return BadRequest(new { message = "Designation is required." });

            var duplicate = await _db.PowerAndAC.AnyAsync(x =>
                x.Id != id && x.Designation == designation && x.Year == request.Year && x.Month == request.Month);
            if (duplicate)
                return Conflict(new { message = "A record already exists for this designation, year, and month." });

            entity.Designation = designation;
            entity.Year = request.Year;
            entity.Month = request.Month;
            entity.Scheduled = request.Scheduled;
            entity.Attended = request.Attended;
            entity.Cumulative_Sched = request.Cumulative_Sched;
            entity.Cumulative_Achieved = request.Cumulative_Achieved;
            entity.IsVerified = false;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return HandleDatabaseWriteError(ex, "update");
            }
            return Ok(ToDto(entity));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.PowerAndAC.FindAsync(id);
            if (entity == null) return NotFound();

            _db.PowerAndAC.Remove(entity);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return HandleDatabaseWriteError(ex, "delete");
            }
            return NoContent();
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

        private static PowerAndACDto ToDto(PowerAndAC entity) => new()
        {
            Id = entity.Id,
            Designation = entity.Designation,
            Year = entity.Year,
            Month = entity.Month,
            Scheduled = entity.Scheduled,
            Attended = entity.Attended,
            Cumulative_Sched = entity.Cumulative_Sched,
            Cumulative_Achieved = entity.Cumulative_Achieved,
            IsVerified = entity.IsVerified
        };

        private IActionResult HandleDatabaseWriteError(DbUpdateException exception, string operation)
        {
            var sqlException = exception.GetBaseException() as SqlException;
            if (sqlException?.Number is 2601 or 2627)
                return Conflict(new { message = "A record already exists for this designation, year, and month." });
            if (sqlException?.Number == 547)
                return Conflict(new { message = "The record cannot be changed because it is referenced by other data." });

            _logger.LogError(exception, "Failed to {Operation} Power & A/C record.", operation);
            return Problem(
                title: "The database could not save the Power & A/C record.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
