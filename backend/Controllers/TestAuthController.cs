/*
 * File: TestAuthController.cs
 * Provides test endpoints to verify authentication and authorization
 * policies including role-based and page-based access control.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    // =========================================================
    // TEST AUTHORIZATION CONTROLLER
    // Used for validating authentication and policy behavior
    // =========================================================
    [Route("api/[controller]")]
    [ApiController]
    public class TestAuthController : ControllerBase
    {
        // =========================================================
        // PUBLIC ENDPOINT
        // Accessible without authentication
        // =========================================================
        [HttpGet("public")]
        public IActionResult GetPublic()
        {
            return Ok("Public endpoint");
        }

        // =========================================================
        // AUTHENTICATED ENDPOINT
        // Requires a valid authenticated user
        // =========================================================
        [HttpGet("authenticated")]
        [Authorize]
        public IActionResult GetAuthenticated()
        {
            // Return user identity and claims for debugging authentication
            return Ok($"Authenticated as {User.Identity?.Name}. Claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}"))}");
        }

        // =========================================================
        // SUPER ADMIN ACCESS
        // Requires SuperAdminOnly policy
        // =========================================================
        [HttpGet("superadmin")]
        [Authorize(Policy = "SuperAdminOnly")]
        public IActionResult GetSuperAdmin()
        {
            return Ok("SuperAdmin access granted");
        }

        // =========================================================
        // ADMIN ACCESS
        // Requires AdminOnly policy
        // =========================================================
        [HttpGet("admin")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetAdmin()
        {
            return Ok("Admin access granted");
        }

        // =========================================================
        // PLATFORM ADMIN ACCESS
        // Requires PlatformAdminOnly policy
        // =========================================================
        [HttpGet("platformadmin")]
        [Authorize(Policy = "PlatformAdminOnly")]
        public IActionResult GetPlatformAdmin()
        {
            return Ok("PlatformAdmin access granted");
        }

        // =========================================================
        // PAGE ACCESS CHECK
        // Requires ViewPagePolicy authorization
        // =========================================================
        [HttpGet("page/{pageId}")]
        [Authorize(Policy = "ViewPagePolicy")]
        public IActionResult GetPage(int pageId)
        {
            return Ok($"Access to page {pageId} granted");
        }

        // =========================================================
        // KPI EDIT ACCESS CHECK
        // Requires EditPlatformKpiPolicy authorization
        // =========================================================
        [HttpPost("kpi/{pageId}/edit")]
        [Authorize(Policy = "EditPlatformKpiPolicy")]
        public IActionResult EditKpi(int pageId)
        {
            return Ok($"Edit access to KPI page {pageId} granted (Date <= 15th)");
        }
    }
}