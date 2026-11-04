/*
 * File: RtomAreaController.cs
 * Provides API endpoint to retrieve RTOM area records from the database.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers
{
    // =========================================================
    // RTOM AREA CONTROLLER
    // Handles retrieval of RTOM area data
    // =========================================================
    [Route("api/rtom-areas")]
    [ApiController]
    [Authorize]
    public class RtomAreaController : ControllerBase
    {
        // Database context for RTOM area data
        private readonly AppDbContext _context;

        // Inject database context
        public RtomAreaController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET ALL RTOM AREAS
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Retrieve all RTOM area records
            return Ok(await _context.RtomArea.ToListAsync());
        }
    }
}