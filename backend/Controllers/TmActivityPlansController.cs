/*
 * File: TmActivityPlansController.cs
 * Provides API endpoints for managing TM Activity Plan records including
 * retrieval, creation, update, and deletion with role and policy-based authorization.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.DTOs;

using Microsoft.AspNetCore.Authorization;
using backend.Helpers.Authorization;

namespace backend.Controllers
{
    // =========================================================
    // TM ACTIVITY PLANS CONTROLLER
    // Handles CRUD operations for TM Activity Plan data
    // =========================================================
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TmActivityPlansController : ControllerBase
    {
        // Database context
        private readonly AppDbContext _context;

        // Authorization service for page-level access checks
        private readonly IAuthorizationService _authorizationService;

        // Page identifier used in authorization policies
        private const int PageId = 5;

        // Inject dependencies
        public TmActivityPlansController(AppDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        // =========================================================
        // GET ALL TM ACTIVITY PLANS
        // Requires ViewPagePolicy authorization
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Check page view permission
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Retrieve all records
            var data = await _context.TmActivity1
                .AsNoTracking()
                .ToListAsync();

            return Ok(data);
        }

        // =========================================================
        // GET TM ACTIVITY PLAN BY ID
        // =========================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            // Check page view permission
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Find record by ID
            var row = await _context.TmActivity1.FindAsync(id);

            if (row == null)
                return NotFound();

            return Ok(row);
        }

        // =========================================================
        // CREATE NEW TM ACTIVITY PLAN
        // Admin OR PlatformAdmin with EditPlatformKpiPolicy
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTmActivityPlanDto dto)
        {
            // Allow Admin/SuperAdmin directly
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

            // PlatformAdmin must pass edit policy
            if (!isAdmin)
            {
                var auth = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
                if (!auth.Succeeded) return Forbid();
            }

            // Validate request body
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Generate UTC timestamps
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            // Create entity from DTO
            var entity = new TmActivity1
            {
                No = byte.TryParse(dto.No, out var noVal) ? noVal : null,
                Kpi = dto.Kpi,
                Target = dto.Target,
                Calculation = dto.Calculation,
                CreatedAt = now,
                UpdatedAt = now
            };

            // Insert new record
            _context.TmActivity1.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(entity);
        }

        // =========================================================
        // UPDATE TM ACTIVITY PLAN
        // Admin OR PlatformAdmin with EditPlatformKpiPolicy
        // =========================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTmActivityPlanDto dto)
        {
            // Allow Admin/SuperAdmin directly
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

            // PlatformAdmin must pass edit policy
            if (!isAdmin)
            {
                var auth = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
                if (!auth.Succeeded) return Forbid();
            }

            // Retrieve existing record
            var entity = await _context.TmActivity1.FindAsync(id);

            if (entity == null)
                return NotFound();

            // Update fields
            entity.No = byte.TryParse(dto.No, out var noVal) ? noVal : entity.No;
            entity.Kpi = dto.Kpi;
            entity.Target = dto.Target;
            entity.Calculation = dto.Calculation;

            // Update modification timestamp
            entity.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =========================================================
        // DELETE TM ACTIVITY PLAN
        // Admin-only endpoint
        // =========================================================
        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            // Find record by ID
            var entity = await _context.TmActivity1.FindAsync(id);

            if (entity == null)
                return NotFound();

            // Remove record
            _context.TmActivity1.Remove(entity);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}