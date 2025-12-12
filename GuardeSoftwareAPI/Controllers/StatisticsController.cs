using GuardeSoftwareAPI.Dtos.Statistics;
using GuardeSoftwareAPI.Services.statistics;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        [HttpGet("monthly")]
        public async Task<ActionResult<MonthlyStatisticsDTO>> GetMonthlyStatistics([FromQuery] int year, [FromQuery] int month)
        {
            if (year < 2000 || year > 2400) return BadRequest("Año inválido.");
            if (month < 1 || month > 12) return BadRequest("Mes inválido.");

            var stats = await _statisticsService.GetMonthlyStatistics(year, month);
            return Ok(stats);
        }

        [HttpGet("client-statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var stats = await _statisticsService.GetClientStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
