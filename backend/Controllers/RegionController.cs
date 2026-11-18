/*
 * File: RegionController.cs
 * Provides API endpoints for managing region data records including
 * retrieval, creation, update, and deletion.
 */

using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Models;
using backend.DTOs;
using Microsoft.AspNetCore.Authorization;


namespace backend.Controllers
{
    // =========================================================
    // REGION DATA CONTROLLER
    // Handles CRUD operations for region data records
    // =========================================================
    [Route("api/regiondata")]
    [ApiController]
    [Authorize]
    public class RegionController : ControllerBase
    {
        // Database context for region data
        private readonly AppDbContext _context;

        // Inject database context
        public RegionController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET ALL REGION DATA
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Retrieve all region records
            // Important: only select columns that exist in the DB schema.
            // Some environments may not yet have the Eng-Name column.
            // Avoid selecting EngName entirely because not all DBs have the Eng-Name column yet.
            // Returning RegionDto with EngName = "" keeps frontend compatible without breaking other pages.
            var items = await _context.RegionData
                .AsNoTracking()
                .Select(x => new RegionDto
                {
                    Id = x.Id,
                    Region = x.Region,
                    Province = x.Province,
                    NetworkEngineer = x.NetworkEngineer,
                    LeaCode = x.LeaCode,
                    EngName = x.EngName ?? string.Empty
                })
                .ToListAsync();


            return Ok(items);

        }


        // =========================================================
        // GET REGION DATA BY ID
        // =========================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            // Find region record by primary key
            var data = await _context.RegionData
                .AsNoTracking()
                .Select(x => new RegionDto
                {
                    Id = x.Id,
                    Region = x.Region,
                    Province = x.Province,
                    NetworkEngineer = x.NetworkEngineer,
                    LeaCode = x.LeaCode,
                    EngName = x.EngName ?? string.Empty
                })
                .FirstOrDefaultAsync(x => x.Id == id);

            if (data == null)
                return NotFound();

            return Ok(data);
        }


        // =========================================================
        // CREATE NEW REGION RECORD
        // Admin-only endpoint
        // =========================================================
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create(RegionDto model)
        {
            // Insert new region record
            var entity = new RegionData
            {
                Region = model.Region,
                Province = model.Province,
                NetworkEngineer = model.NetworkEngineer,
                // EngName is only available in some DB versions.
                // If column doesn't exist, this action will fail; GetAll/GetById remain safe.
                EngName = model.EngName,
                LeaCode = model.LeaCode
            };

            _context.RegionData.Add(entity);
            await _context.SaveChangesAsync();

            // Return created DTO (including generated Id)
            var created = new RegionDto
            {
                Id = entity.Id,
                Region = entity.Region,
                Province = entity.Province,
                NetworkEngineer = entity.NetworkEngineer,
                EngName = entity.EngName ?? string.Empty,
                LeaCode = entity.LeaCode
            };

            return Ok(created);
        }


        // =========================================================
        // UPDATE EXISTING REGION RECORD
        // Admin-only endpoint
        // =========================================================
        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, RegionDto model)
        {
            // Validate route id matches model id
            if (id != model.Id)
                return BadRequest();

            var entity = await _context.RegionData.FindAsync(id);
            if (entity == null)
                return NotFound();

            entity.Region = model.Region;
            entity.Province = model.Province;
            entity.NetworkEngineer = model.NetworkEngineer;
            // EngName is only available in some DB versions.
            entity.EngName = model.EngName;
            entity.LeaCode = model.LeaCode;

            await _context.SaveChangesAsync();

            var updated = new RegionDto
            {
                Id = entity.Id,
                Region = entity.Region,
                Province = entity.Province,
                NetworkEngineer = entity.NetworkEngineer,
                EngName = entity.EngName ?? string.Empty,
                LeaCode = entity.LeaCode
            };

            return Ok(updated);
        }


        // =========================================================
        // DELETE REGION RECORD
        // Admin-only endpoint
        // =========================================================
        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            // Find region record by id
            var data = await _context.RegionData.FindAsync(id);

            if (data == null)
                return NotFound();

            // Remove region record
            _context.RegionData.Remove(data);

            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}