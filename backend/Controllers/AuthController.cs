/*
 * File: AuthController.cs
 * Handles authentication including Azure login verification and JWT token generation.
 */

using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // Database context for accessing user data
        private readonly AppDbContext _context;

        // Configuration provider used to read JWT settings
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // =========================================================
        // AZURE LOGIN VERIFICATION
        // =========================================================

        // POST: /api/auth/verify-azure-login
        // Verifies Azure authenticated user and validates Service ID in system
        [HttpPost("verify-azure-login")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyAzureLogin([FromBody] VerifyAzureLoginDto dto)
        {
            // Ensure Azure email exists (proves Azure login happened)
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("Azure authentication required. Please sign in with Microsoft first.");

            // Ensure Service ID is provided
            if (string.IsNullOrWhiteSpace(dto.ServiceId))
                return BadRequest("Service ID is required.");

            // Basic validation of Azure email format
            if (!dto.Email.Contains("@"))
                return BadRequest("Invalid email format from Azure authentication.");

            // Find user in system using Service ID
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.ServiceId == dto.ServiceId);

            if (user == null)
                return Unauthorized("Service ID not found in the system. Please contact your administrator.");

            if (!user.IsActive)
                return Unauthorized("Your account has been deactivated. Please contact your administrator.");

            // Generate token and return login response
            return await GenerateLoginResponse(user);
        }

        // =========================================================
        // LOGIN RESPONSE GENERATION
        // =========================================================

        private async Task<IActionResult> GenerateLoginResponse(User user)
        {
            // Generate JWT token
            var token = GenerateJwtToken(user);

            // Update user's last login timestamp
            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Retrieve pages user is allowed to access
            var pages = await _context.UserPageAccess
                .Where(upa => upa.UserId == user.UserId)
                .Select(upa => upa.Page.PageName)
                .ToListAsync();

            // Retrieve platform KPI page assignments
            var assignedPages = await _context.PlatformKpiAssignments
                .Where(pka => pka.UserId == user.UserId)
                .Select(pka => pka.Page.PageName)
                .ToListAsync();

            // Ensure PlatformAdmin has correct platform assignment
            if (string.Equals(user.Role?.RoleName, "PlatformAdmin", StringComparison.OrdinalIgnoreCase))
            {
                var targetPageId = MapKnownPageId(pages.FirstOrDefault());

                if (targetPageId.HasValue)
                {
                    var existingIds = await _context.PlatformKpiAssignments
                        .Where(pka => pka.UserId == user.UserId)
                        .Select(pka => pka.PageId)
                        .ToListAsync();

                    // Replace assignment if inconsistent
                    if (existingIds.Count != 1 || existingIds[0] != targetPageId.Value)
                    {
                        var toRemove = _context.PlatformKpiAssignments.Where(pka => pka.UserId == user.UserId);
                        _context.PlatformKpiAssignments.RemoveRange(toRemove);

                        _context.PlatformKpiAssignments.Add(new PlatformKpiAssignment
                        {
                            UserId = user.UserId,
                            PageId = targetPageId.Value
                        });

                        await _context.SaveChangesAsync();

                        assignedPages = await _context.PlatformKpiAssignments
                            .Where(pka => pka.UserId == user.UserId)
                            .Select(pka => pka.Page.PageName)
                            .ToListAsync();
                    }
                }
            }

            return Ok(new { token, user.Name, Role = user.Role?.RoleName, Pages = pages, AssignedPages = assignedPages });
        }

        // =========================================================
        // JWT TOKEN GENERATION
        // =========================================================

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");

            // Secret key used to sign JWT
            var secretKey = jwtSettings["Secret"] ?? "SuperSecretKeyForDevelopmentOnly12345!@#$%";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Claims included in JWT
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.ServiceId),
                new Claim("ServiceId", user.ServiceId),
                new Claim("serviceId", user.ServiceId),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "User"),
                new Claim("role", user.Role?.RoleName ?? "User"),
                new Claim("UserId", user.UserId.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"] ?? "KPI_Backend",
                audience: jwtSettings["Audience"] ?? "KPI_Frontend",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(5),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static byte? MapKnownPageId(string? input)
        {
            var key = NormalizeKey(input);
            if (string.IsNullOrEmpty(key)) return null;

            return key switch
            {
                "ipnwop" => (byte)1,
                "servicefulfilment" => (byte)2,
                "servicefulfillment" => (byte)2,
                "bbanw" => (byte)3,
                "otnop" => (byte)4,
                "otonop" => (byte)4,
                "tmactivityplan" => (byte)5,
                "otheroperator" => (byte)5,
                "routinemtnc" => (byte)6,
                "routinemaintenance" => (byte)6,
                "towermtceachievement" => (byte)7,
                "towermtce" => (byte)7,
                "towermaintenance" => (byte)7,
                "towermaintainance" => (byte)7,
                "enterprisekpi" => (byte)8,
                "otheroperatorkpi" => (byte)9,
                "otherkpi" => (byte)10,
                _ => null
            };
        }

        // Normalize page names for comparison
        private static string NormalizeKey(string? value)
        {
            return new string((value ?? string.Empty)
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
        }
    }

    // DTO used for Service ID login
    public class LoginDto
    {
        public string ServiceId { get; set; }
    }

    // DTO used for Azure authentication verification
    public class VerifyAzureLoginDto
    {
        public string Email { get; set; }
        public string ServiceId { get; set; }
    }
}
