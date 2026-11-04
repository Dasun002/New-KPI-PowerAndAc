/*
 * File: ClaimsTransformation.cs
 * Implements Azure AD claims transformation to extract user information from tokens
 * and populate custom claims for authorization logic.
 */

using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Helpers
{
    // =========================================================
    // CLAIMS TRANSFORMATION
    // Transforms Azure AD token claims into application-specific claims
    // =========================================================
    public class ClaimsTransformation : IClaimsTransformation
    {
        // Database context for accessing user and assignment data
        private readonly AppDbContext _context;

        public ClaimsTransformation(AppDbContext context)
        {
            _context = context;
        }

        // Transforms the principal by extracting user info and adding custom claims
        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            // Avoid running transformation if already done
            if (principal.HasClaim(c => c.Type == "TransformationMatch"))
            {
                return principal;
            }

            var identity = (ClaimsIdentity)principal.Identity!;
            
            // Extract ServiceId from Azure AD token
            // Assuming the claim type for ServiceId is "serviceId" as requested or fallback
            var serviceIdClaim = principal.FindFirst("serviceId")
                                 ?? principal.FindFirst("ServiceId")
                                 ?? principal.FindFirst(ClaimTypes.NameIdentifier)
                                 ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
                                 ?? principal.FindFirst("preferred_username"); 
            
            var serviceId = serviceIdClaim?.Value;

            if (string.IsNullOrEmpty(serviceId))
            {
                return principal; // Cannot identify user
            }

            // In some cases serviceId might be email, extract part before @ if needed, but assuming full match for now
            // based on prompt: "Extract ServiceId from token (assume claim type: "serviceId")"

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.ServiceId == serviceId);

            if (user == null || !user.IsActive)
            {
                // User not found or inactive. 
                return principal;
            }

            // Update Last Login
            // Note: This causes a DB write on every request. Ensure this is acceptable performance-wise.
            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Add Custom Claims
            if (user.Role != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.RoleName));
                identity.AddClaim(new Claim("role", user.Role.RoleName)); 
            }

            identity.AddClaim(new Claim("serviceId", user.ServiceId));
            identity.AddClaim(new Claim("UserId", user.UserId.ToString()));

            // Allowed Pages - pages the user can access
            var allowedPages = await _context.UserPageAccess
                .Where(upa => upa.UserId == user.UserId)
                .Select(upa => upa.PageId)
                .ToListAsync();

            foreach (var pageId in allowedPages)
            {
                identity.AddClaim(new Claim("allowedPages", pageId.ToString()));
            }

            // Assigned KPI Pages - pages the user can edit (Platform KPI only)
            var assignedKpiPages = await _context.PlatformKpiAssignments
                .Where(pka => pka.UserId == user.UserId)
                .Select(pka => pka.PageId)
                .ToListAsync();

            foreach (var pageId in assignedKpiPages)
            {
                identity.AddClaim(new Claim("assignedKpiPages", pageId.ToString()));
            }

            identity.AddClaim(new Claim("TransformationMatch", "true"));

            return principal;
        }
    }
}
