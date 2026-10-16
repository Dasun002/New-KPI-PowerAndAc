/*
 * File: RegionController.cs
 * Provides API endpoints for managing region data records including
 * retrieval, creation, update, and deletion.
 *
 * NOTE ABOUT EngName
 * Some databases do not have the EngName column yet. To keep the dashboard
 * working on those databases:
 *   - GET endpoints never read x.EngName (they return an empty string).
 *   - PUT uses ExecuteUpdateAsync, so only the listed columns are touched.
 *   - DELETE uses ExecuteDeleteAsync, so no entity (and no EngName) is loaded.
 *   - POST still needs the EngName column because EF inserts the whole entity.
 *     Add the column first:
 *       IF COL_LENGTH('dbo.RegionData','EngName') IS NULL
 *           ALTER TABLE dbo.RegionData ADD EngName NVARCHAR(200) NULL;
 *     After the column exists, you can switch the GET endpoints back to
 *     "EngName = x.EngName ?? string.Empty" (see the marked lines).
 */

using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var items = await _context.RegionData
                .AsNoTracking()
                .Select(x => new RegionDto
                {
                    Id = x.Id,
                    Region = x.Region,
                    Province = x.Province,
                    NetworkEngineer = x.NetworkEngineer,
                    LeaCode = x.LeaCode,

                    // EngName column not read, so this works on databases without it.
                    // Once the column exists, you may use: x.EngName ?? string.Empty
                    EngName = string.Empty
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
            // Filter first, then project, so only one row is read
            var data = await _context.RegionData
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new RegionDto
                {
                    Id = x.Id,
                    Region = x.Region,
                    Province = x.Province,
                    NetworkEngineer = x.NetworkEngineer,
                    LeaCode = x.LeaCode,

                    // Same rule as GetAll: avoid the EngName column for now
                    EngName = string.Empty
                })
                .FirstOrDefaultAsync();

            if (data == null)
                return NotFound();

            return Ok(data);
        }

        // =========================================================
        // CREATE NEW REGION RECORD
        // Admin-only endpoint
        // Requires the EngName column to exist (EF inserts all mapped columns)
        // =========================================================
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create(RegionDto model)
        {
            var entity = new RegionData
            {
                Region = model.Region,
                Province = model.Province,
                NetworkEngineer = model.NetworkEngineer,
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
        // Updates only the listed columns, so it works without EngName
        // =========================================================
        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, RegionDto model)
        {
            // Validate route id matches model id
            if (id != model.Id)
                return BadRequest();

            var rows = await _context.RegionData
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Region, model.Region)
                    .SetProperty(x => x.Province, model.Province)
                    .SetProperty(x => x.NetworkEngineer, model.NetworkEngineer)
                    .SetProperty(x => x.LeaCode, model.LeaCode));

            if (rows == 0)
                return NotFound();

            // Return the updated values (EngName left empty, see note at top)
            var updated = new RegionDto
            {
                Id = id,
                Region = model.Region,
                Province = model.Province,
                NetworkEngineer = model.NetworkEngineer,
                EngName = model.EngName ?? string.Empty,
                LeaCode = model.LeaCode
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
            var rows = await _context.RegionData
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();

            if (rows == 0)
                return NotFound();

            return Ok();
        }
    }
}