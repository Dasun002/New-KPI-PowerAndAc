/*
 * File: MultiTableController.cs
 * Exposes API endpoints to retrieve platform-specific KPI data
 * from multiple tables via the MultiTable service layer.
 */

using Microsoft.AspNetCore.Mvc;
using backend.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Controllers
{
    // =========================================================
    // MULTI TABLE CONTROLLER
    // Provides endpoints for fetching KPI data from different
    // platform-specific tables (MSAN, VPN, SLBN)
    // =========================================================
    [ApiController]
    [Route("api/multi-table")]
    public class MultiTableController : ControllerBase
    {
        // Service responsible for retrieving platform data
        private readonly IMultiTableService _multiTableService;

        // Inject service dependency
        public MultiTableController(IMultiTableService multiTableService)
        {
            _multiTableService = multiTableService;
        }

        // =========================================================
        // FETCH MSAN PLATFORM DATA
        // =========================================================
        [HttpGet("fetchMsan")]
        public async Task<IActionResult> FetchMsan([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var data = await _multiTableService.FetchMsanDataAsync(year, month);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("fetchVpn")]
        public async Task<IActionResult> FetchVpn([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var data = await _multiTableService.FetchVpnDataAsync(year, month);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("fetchSlbn")]
        public async Task<IActionResult> FetchSlbn([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var data = await _multiTableService.FetchSlbnDataAsync(year, month);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }




    }
}