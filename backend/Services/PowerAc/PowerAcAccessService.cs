/*
 * File: PowerAcAccessService.cs
 * Authorization rules for the Power & AC KPI, built on the existing role claims, ViewPagePolicy
 * and EditPlatformKpiPolicy (page 11 = POWER_AC). No new authentication mechanism is introduced.
 *
 *   View results and data            ViewPagePolicy for page 11
 *   Enter allocations / data          Admin, SuperAdmin, or PlatformAdmin with page 11 edit rights
 *                                     AND an assignment to that engineer group
 *   Verify submitted data             SuperAdmin, or PlatformAdmin with page 11 edit rights; never the submitter
 *   Calculate and save results        Admin, SuperAdmin, or PlatformAdmin with page 11 edit rights
 *   Configuration (weights, scoring)  AdminOnly policy on the controller
 */
using System.Security.Claims;
using backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.PowerAc
{
    public class PowerAcAccessService
    {
        public const int PowerAcPageId = 11;

        private readonly AppDbContext _db;
        private readonly IAuthorizationService _authorization;

        public PowerAcAccessService(AppDbContext db, IAuthorizationService authorization)
        {
            _db = db;
            _authorization = authorization;
        }

        public static bool HasRole(ClaimsPrincipal user, string role) =>
            user.Claims.Any(c => (c.Type == "role" || c.Type == ClaimTypes.Role)
                && string.Equals(c.Value?.Replace(" ", string.Empty), role, StringComparison.OrdinalIgnoreCase));

        public static bool IsAdmin(ClaimsPrincipal user) => HasRole(user, "Admin") || HasRole(user, "SuperAdmin");

        public static string? CurrentUserName(ClaimsPrincipal user) =>
            user.FindFirst("serviceId")?.Value
            ?? user.FindFirst("ServiceId")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.Identity?.Name;

        public static int? CurrentUserId(ClaimsPrincipal user) =>
            int.TryParse(user.FindFirst("UserId")?.Value, out var id) ? id : null;

        public async Task<bool> CanViewAsync(ClaimsPrincipal user) =>
            (await _authorization.AuthorizeAsync(user, PowerAcPageId, "ViewPagePolicy")).Succeeded;

        public async Task<bool> HasPageEditRightsAsync(ClaimsPrincipal user) =>
            (await _authorization.AuthorizeAsync(user, PowerAcPageId, "EditPlatformKpiPolicy")).Succeeded;

        public async Task<bool> CanCalculateAsync(ClaimsPrincipal user) =>
            IsAdmin(user) || await HasPageEditRightsAsync(user);

        public async Task<bool> CanVerifyAsync(ClaimsPrincipal user) =>
            HasRole(user, "SuperAdmin") || await HasPageEditRightsAsync(user);

        // Engineer groups the user may enter data for.
        public async Task<HashSet<int>> GetManageableGroupIdsAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            if (IsAdmin(user))
                return (await _db.PowerAcEngineerGroups.AsNoTracking().Select(g => g.Id).ToListAsync(ct)).ToHashSet();

            if (!await HasPageEditRightsAsync(user))
                return new HashSet<int>();

            var userId = CurrentUserId(user);
            if (userId is null)
                return new HashSet<int>();

            return (await _db.PowerAcGroupUserAssignments.AsNoTracking()
                    .Where(a => a.UserId == userId.Value)
                    .Select(a => a.EngineerGroupId)
                    .ToListAsync(ct))
                .ToHashSet();
        }

        public async Task<bool> CanManageGroupAsync(ClaimsPrincipal user, int engineerGroupId, CancellationToken ct = default) =>
            (await GetManageableGroupIdsAsync(user, ct)).Contains(engineerGroupId);
    }
}
