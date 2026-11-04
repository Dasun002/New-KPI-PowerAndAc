/*
 * File: KpiController.cs
 * Provides API endpoints for managing KPI records.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers
{
    // =========================================================
    // KPI CONTROLLER
    // Handles authorized API requests related to KPI operations
    // =========================================================
    [ApiController]
    [Route("api/kpis")]
    [Authorize]
    public class KpiController : ControllerBase
    {
        // Database context used to access KPI data
        private readonly AppDbContext _context;

        // Inject application database context
        public KpiController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // KPI API ENDPOINTS
        // Additional KPI CRUD operations will be implemented here
        // =========================================================
    }
}