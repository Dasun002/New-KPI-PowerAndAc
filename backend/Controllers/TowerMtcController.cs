using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/tower-mtc")]
    public class TowerMtcController : Controller
    {
        private readonly IMultiTableService _multiTableService;

        // FIX: Inject service here
        public TowerMtcController(IMultiTableService multiTableService)
        {
            _multiTableService = multiTableService;
        }

        [HttpGet("fetchTower")]
        public async Task<IActionResult> FetchTower([FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var data = await _multiTableService.FetchTowerDataAsync(year, month);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}