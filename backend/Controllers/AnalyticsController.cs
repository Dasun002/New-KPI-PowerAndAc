using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/analytics")]
    [Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analyticsService;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet]
        public async Task<ActionResult<List<AnalyticsResultDto>>> GetCumulativeAnalytics(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth)
        {
            if (startMonth < 1 || startMonth > 12 ||
                endMonth < 1 || endMonth > 12)
            {
                return BadRequest("Months must be between 1 and 12.");
            }

            if (startMonth > endMonth)
            {
                return BadRequest("Start month must be less than or equal to end month.");
            }

            var results = await _analyticsService.GetCumulativeAnalyticsAsync(year, startMonth, endMonth);
            return Ok(results);
        }

        [HttpGet("debug")]
        [AllowAnonymous]
        public async Task<IActionResult> Debug([FromServices] backend.Data.AppDbContext db)
        {
            var kpis = await db.KpiDefinitions.ToListAsync();
            var results = await db.OverallKpiResults.Where(x => x.Month == 4).Take(50).ToListAsync();
            return Ok(new { Kpis = kpis, Results = results });
        }

        [HttpGet("years")]
        public async Task<ActionResult<List<int>>> GetAvailableYears()
        {
            var years = await _analyticsService.GetAvailableYearsAsync();
            return Ok(years);
        }

        [HttpGet("months")]
        public async Task<ActionResult<List<int>>> GetAvailableMonths([FromQuery] short year)
        {
            var months = await _analyticsService.GetAvailableMonthsAsync(year);
            return Ok(months);
        }
    }
}
