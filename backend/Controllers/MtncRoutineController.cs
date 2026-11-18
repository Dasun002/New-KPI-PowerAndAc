﻿/*
 * File: MtncRoutineController.cs
 * Provides API endpoints for managing Routine Maintenance KPI records,
 * including retrieval, creation, update, and deletion with authorization checks.
 */

using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;
using backend.Helpers.Authorization;

namespace backend.Controllers
{
    // =========================================================
    // ROUTINE MAINTENANCE KPI CONTROLLER
    // Handles CRUD operations for Routine Maintenance KPIs
    // =========================================================
    [ApiController]
    [Route("api/mtnc-routine")]
    [Authorize]
    public class MtncRoutineController : ControllerBase
    {
        // Database context for Routine Maintenance KPI data
        private readonly AppDbContext _db;

        // Authorization service for page-level permission checks
        private readonly IAuthorizationService _authorizationService;

        // Page identifier used for authorization policies
        private const int PageId = 6;

        // Inject dependencies
        public MtncRoutineController(AppDbContext db, IAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
        }

        // =========================================================
        // GET ALL ROUTINE MAINTENANCE KPIs
        // Requires ViewPagePolicy authorization
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Verify user has permission to view this page
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            // Retrieve KPI records ordered by KPI number
            var list = await _db.MtncRoutines
                .AsNoTracking()
                .OrderBy(x => x.No)
                .Select(x => new
                {
                    _id = x.Id,
                    no = x.No,
                    kpi = x.Kpi,
                    target = x.Target,
                    calculation = x.Calculation,
                    platform = x.Platform,
                    responsibleDGM = x.ResponsibleDGM,
                    definedOLADetails = x.DefinedOLADetails,
                    dataSources = x.DataSources
                })
                .ToListAsync();

            return Ok(list);
        }

        // =========================================================
        // CREATE NEW ROUTINE MAINTENANCE KPI
        // Requires Admin/SuperAdmin or EditPlatformKpiPolicy
        // =========================================================
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] MtncRoutineDto dto)
        {
            // Allow admins directly, otherwise check edit permission
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
            if (!isAdmin)
            {
                var auth = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
                if (!auth.Succeeded) return Forbid();
            }

            // Validate request body
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Create entity from DTO
            var entity = new MtncRoutine
            {
                No = dto.No,
                Kpi = dto.Kpi.Trim(),
                Target = dto.Target.Trim(),
                Calculation = dto.Calculation.Trim(),
                Platform = dto.Platform.Trim(),
                ResponsibleDGM = dto.ResponsibleDGM.Trim(),
                DefinedOLADetails = dto.DefinedOLADetails.Trim(),
                DataSources = dto.DataSources.Trim(),

                // Store timestamps in ISO UTC format
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),

                // Initial version number
                V = 0
            };

            // Insert new record
            _db.MtncRoutines.Add(entity);
            await _db.SaveChangesAsync();

            // Return created entity identifier
            return Ok(new { _id = entity.Id });
        }

        // =========================================================
        // UPDATE ROUTINE MAINTENANCE KPI
        // Requires Admin/SuperAdmin or EditPlatformKpiPolicy
        // =========================================================
        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] MtncRoutineDto dto)
        {
            // Allow admins directly, otherwise check edit permission
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
            if (!isAdmin)
            {
                var auth = await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
                if (!auth.Succeeded) return Forbid();
            }

            // Validate request body
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Retrieve existing KPI record
            var entity = await _db.MtncRoutines.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound(new { message = "KPI not found." });

            // Update KPI fields
            entity.No = dto.No;
            entity.Kpi = dto.Kpi.Trim();
            entity.Target = dto.Target.Trim();
            entity.Calculation = dto.Calculation.Trim();
            entity.Platform = dto.Platform.Trim();
            entity.ResponsibleDGM = dto.ResponsibleDGM.Trim();
            entity.DefinedOLADetails = dto.DefinedOLADetails.Trim();
            entity.DataSources = dto.DataSources.Trim();

            // Update modification timestamp
            entity.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            // Increment version number
            entity.V = (byte)(entity.V + 1);

            await _db.SaveChangesAsync();

            // Return updated entity identifier
            return Ok(new { _id = entity.Id });
        }

        // =========================================================
        // DELETE ROUTINE MAINTENANCE KPI
        // Admin-only endpoint
        // =========================================================
        [HttpDelete("delete/{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            // Find KPI record by ID
            var entity = await _db.MtncRoutines.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound(new { message = "KPI not found." });

            // Remove record from database
            _db.MtncRoutines.Remove(entity);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}