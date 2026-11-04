using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/ipnw-mtc-data")]
    public class IpnwMtcDataController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationService _authorizationService;
        private const int PageId = 6; // Or whichever page ID applies

        public IpnwMtcDataController(AppDbContext db, IAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }

        private async Task<AuthorizationResult> AuthorizeEditAsync()
        {
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
            {
                return AuthorizationResult.Success();
            }

            return await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
        }

        [HttpPatch("{id:int}/toggle-verified")]
        public async Task<IActionResult> ToggleVerified(int id)
        {
            // Only toggle if authorized (for now allow for simplicity if auth fails elsewhere, but keep the check)
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
            if (!authResult.Succeeded) return Forbid();

            var entity = await _db.IpnwMtcData
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.IsVerified })
                .FirstOrDefaultAsync();
            if (entity == null) return NotFound();

            var newValue = !entity.IsVerified;

            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE dbo.ipnwmtcdata SET [is_verified] = @p0 WHERE [id] = @p1",
                newValue,
                id);
            
            return Ok(new { id = entity.Id, isVerified = newValue });
        }
    }
}
