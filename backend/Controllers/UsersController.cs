/*
 * File: UsersController.cs
 * Provides API endpoints for managing users, roles, page access,
 * and platform KPI assignments within the system.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

namespace backend.Controllers
{
    // =========================================================
    // USERS CONTROLLER
    // Handles user management including roles, page access,
    // status updates, and platform KPI assignments
    // =========================================================
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "AdminOnly")]
    public class UsersController : ControllerBase
    {
        // Database context
        private readonly AppDbContext _db;

        // Inject database context
        public UsersController(AppDbContext db)
        {
            _db = db;
        }

        // =========================================================
        // GET ALL USERS
        // =========================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            try
            {
                // Load users with their roles
                var users = await _db.Users
                    .Include(u => u.Role)
                    .ToListAsync();

                // Load all page access entries to map pages per user
                var allAccess = await _db.UserPageAccess
                    .Include(upa => upa.Page)
                    .ToListAsync();

                // Map database entities to DTO objects
                var result = users.Select(u => new UserDto
                {
                    UserId = u.UserId,
                    ServiceId = u.ServiceId,
                    Name = u.Name,
                    Role = u.Role?.RoleName ?? "",
                    IsActive = u.IsActive,
                    LastLogin = u.LastLogin,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    Pages = allAccess
                        .Where(a => a.UserId == u.UserId)
                        .Select(a => a.Page?.PageName ?? "")
                        .ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        // =========================================================
        // GET USER BY ID
        // =========================================================
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            try
            {
                // Retrieve user with role information
                var user = await _db.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(x => x.UserId == id);

                if (user == null) return NotFound();

                // Retrieve page access for the user
                var pages = await _db.UserPageAccess
                    .Where(upa => upa.UserId == id)
                    .Select(upa => upa.Page != null ? upa.Page.PageName : "")
                    .ToListAsync();

                return new UserDto
                {
                    UserId = user.UserId,
                    ServiceId = user.ServiceId,
                    Name = user.Name,
                    Role = user.Role?.RoleName ?? "",
                    IsActive = user.IsActive,
                    LastLogin = user.LastLogin,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    Pages = pages
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        // =========================================================
        // CREATE USER
        // =========================================================
        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto dto)
        {
            try
            {
                // Locate role from database
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == dto.Role);
                if (role == null)
                {
                    return BadRequest(new { message = $"Role '{dto.Role}' not found." });
                }

                // Create new user entity
                var user = new User
                {
                    ServiceId = dto.ServiceId,
                    Name = dto.Name,
                    RoleId = role.RoleId,
                    IsActive = dto.IsActive,
                    CreatedAt = DateTime.UtcNow,

                    // Generate internal email to satisfy unique constraint
                    Email = $"{dto.ServiceId}@internal.slt"
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                // Assign page access permissions if provided
                if (dto.Pages != null && dto.Pages.Any())
                {
                    foreach (var pageName in dto.Pages)
                    {
                        var page = await FindPageByLooseMatch(pageName);
                        if (page != null)
                        {
                            _db.UserPageAccess.Add(new UserPageAccess
                            {
                                UserId = user.UserId,
                                PageId = page.PageId
                            });
                        }
                    }
                }

                // Synchronize platform KPI assignments for PlatformAdmin users
                await SyncPlatformAssignments(user.UserId, role.RoleName, dto.Pages);

                await _db.SaveChangesAsync();

                return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, new UserDto
                {
                    UserId = user.UserId,
                    ServiceId = user.ServiceId,
                    Name = user.Name,
                    Role = role.RoleName,
                    IsActive = user.IsActive,
                    Pages = dto.Pages ?? new List<string>(),
                    CreatedAt = user.CreatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error creating user: {ex.Message}" });
            }
        }

        // =========================================================
        // UPDATE USER
        // =========================================================
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateUser(int id, UpdateUserDto dto)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == id);
                if (user == null) return NotFound();

                // Update user basic information
                if (!string.IsNullOrEmpty(dto.ServiceId)) user.ServiceId = dto.ServiceId;
                if (!string.IsNullOrEmpty(dto.Name)) user.Name = dto.Name;
                if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

                // Update role if provided
                if (!string.IsNullOrEmpty(dto.Role))
                {
                    var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == dto.Role);
                    if (role != null)
                    {
                        user.RoleId = role.RoleId;
                    }
                }

                user.UpdatedAt = DateTime.UtcNow;

                // Update page access if provided
                if (dto.Pages != null)
                {
                    var existing = _db.UserPageAccess.Where(x => x.UserId == id);
                    _db.UserPageAccess.RemoveRange(existing);

                    foreach (var pageName in dto.Pages)
                    {
                        var page = await FindPageByLooseMatch(pageName);
                        if (page != null)
                        {
                            _db.UserPageAccess.Add(new UserPageAccess
                            {
                                UserId = user.UserId,
                                PageId = page.PageId
                            });
                        }
                    }
                }

                // Determine final role for assignment logic
                var finalRoleName = (await _db.Roles
                    .Where(r => r.RoleId == user.RoleId)
                    .Select(r => r.RoleName)
                    .FirstOrDefaultAsync()) ?? string.Empty;

                List<string>? pagesForAssignments = null;

                if (dto.Pages != null)
                {
                    pagesForAssignments = dto.Pages;
                }
                else if (string.Equals(finalRoleName, "PlatformAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    pagesForAssignments = await _db.UserPageAccess
                        .Where(x => x.UserId == id)
                        .Join(_db.Pages,
                              upa => upa.PageId,
                              p => p.PageId,
                              (upa, p) => p.PageName)
                        .ToListAsync();
                }

                // Synchronize platform assignments
                await SyncPlatformAssignments(user.UserId, finalRoleName, pagesForAssignments);

                await _db.SaveChangesAsync();

                return Ok(new { message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating user: {ex.Message}");
            }
        }

        // =========================================================
        // DELETE USER
        // =========================================================
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == id);
                if (user == null) return NotFound();

                _db.Users.Remove(user);
                await _db.SaveChangesAsync();

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting user: {ex.Message}");
            }
        }

        // =========================================================
        // GET ADMIN USERS ONLY
        // =========================================================
        [HttpGet("admins")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAdmins()
        {
            try
            {
                var users = await _db.Users
                    .Include(u => u.Role)
                    .Where(u => u.Role != null && (u.Role.RoleName == "SuperAdmin" || u.Role.RoleName == "Admin"))
                    .ToListAsync();

                var allAccess = await _db.UserPageAccess
                    .Include(upa => upa.Page)
                    .ToListAsync();

                var result = users.Select(u => new UserDto
                {
                    UserId = u.UserId,
                    ServiceId = u.ServiceId,
                    Name = u.Name,
                    Role = u.Role?.RoleName ?? "",
                    IsActive = u.IsActive,
                    LastLogin = u.LastLogin,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    Pages = allAccess
                        .Where(a => a.UserId == u.UserId)
                        .Select(a => a.Page?.PageName ?? "")
                        .ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching admins: {ex.Message}");
            }
        }

        // =========================================================
        // CREATE ADMIN USER
        // =========================================================
        [HttpPost("admins")]
        public async Task<ActionResult<UserDto>> CreateAdmin(CreateUserDto dto)
        {
            // Normalize role name for admin creation
            if (string.Equals(dto.Role, "admin", StringComparison.OrdinalIgnoreCase))
                dto.Role = "Admin";

            return await CreateUser(dto);
        }

        // =========================================================
        // TOGGLE USER ACTIVE STATUS
        // =========================================================
        [HttpPatch("{id}/status")]
        public async Task<ActionResult> ToggleStatus(int id)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == id);
                if (user == null) return NotFound();

                // Toggle active state
                user.IsActive = !user.IsActive;
                user.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new { message = $"User status changed to {(user.IsActive ? "Active" : "Inactive")}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating status: {ex.Message}");
            }
        }

        // =========================================================
        // DEMOTE ADMIN TO STANDARD USER
        // =========================================================
        [HttpPatch("{id}/demote")]
        public async Task<ActionResult> DemoteAdmin(int id)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == id);
                if (user == null) return NotFound();

                var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
                if (userRole == null) return StatusCode(500, "User role not found.");

                user.RoleId = userRole.RoleId;
                user.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new { message = "Admin demoted to User successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error demoting admin: {ex.Message}");
            }
        }

        // =========================================================
        // PROMOTE USER TO ADMIN
        // =========================================================
        [HttpPatch("{id}/promote")]
        public async Task<ActionResult> PromoteToAdmin(int id)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == id);
                if (user == null) return NotFound();

                var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
                if (adminRole == null) return StatusCode(500, "Admin role not found.");

                user.RoleId = adminRole.RoleId;
                user.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new { message = "User promoted to Admin successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error promoting user: {ex.Message}");
            }
        }

        // =========================================================
        // SYNCHRONIZE PLATFORM KPI ASSIGNMENTS
        // =========================================================
        private async Task SyncPlatformAssignments(int userId, string roleName, List<string>? pageNames)
        {
            // Remove existing assignments
            var existingAssignments = _db.PlatformKpiAssignments.Where(x => x.UserId == userId);
            _db.PlatformKpiAssignments.RemoveRange(existingAssignments);

            // Only PlatformAdmin users receive assignments
            if (string.IsNullOrWhiteSpace(roleName) ||
                !string.Equals(roleName, "PlatformAdmin", StringComparison.OrdinalIgnoreCase))
                return;

            if (pageNames == null || !pageNames.Any())
                return;

            // Assign only one platform page
            foreach (var pageName in pageNames)
            {
                var mappedPageId = MapKnownPageId(pageName);

                if (mappedPageId.HasValue)
                {
                    _db.PlatformKpiAssignments.Add(new PlatformKpiAssignment
                    {
                        UserId = userId,
                        PageId = (byte)mappedPageId.Value
                    });
                    break;
                }

                var page = await FindPageByLooseMatch(pageName);

                if (page != null)
                {
                    _db.PlatformKpiAssignments.Add(new PlatformKpiAssignment
                    {
                        UserId = userId,
                        PageId = page.PageId
                    });
                    break;
                }
            }
        }

        // =========================================================
        // NORMALIZE STRING KEY FOR COMPARISON
        // =========================================================
        private static string NormalizeKey(string? value)
        {
            return new string((value ?? string.Empty)
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
        }

        // =========================================================
        // MAP KNOWN PAGE NAMES TO PAGE IDs
        // =========================================================
        private static int? MapKnownPageId(string? input)
        {
            var key = NormalizeKey(input);
            if (string.IsNullOrEmpty(key)) return null;

            return key switch
            {
                "ipnwop" => 1,
                "servicefulfilment" => 2,
                "servicefulfillment" => 2,
                "bbanw" => 3,
                "otnop" => 4,
                "otonop" => 4,
               
                "routinemtnc" => 6,
                "routinemaintenance" => 6,
                "towermtceachievement" => 7,
                "towermtce" => 7,
                "towermaintenance" => 7,
                "towermaintainance" => 7,
                "enterprisekpi" => 8,
                "otheroperatorkpi" => 9,
                "otherkpi" => 10,
                "powerac" => 11,
                
                _ => null
            };
        }

        // =========================================================
        // FIND PAGE USING FLEXIBLE NAME MATCHING
        // =========================================================
        private async Task<Page?> FindPageByLooseMatch(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var normalized = NormalizeKey(input);
            if (string.IsNullOrEmpty(normalized)) return null;

            var pages = await _db.Pages.ToListAsync();

            return pages.FirstOrDefault(p =>
                NormalizeKey(p.PageName) == normalized ||
                NormalizeKey(p.PageCode) == normalized);
        }
    }
}
