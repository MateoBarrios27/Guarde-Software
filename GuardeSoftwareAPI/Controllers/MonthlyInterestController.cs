using GuardeSoftwareAPI.Dtos.MonthlyInterest;
using GuardeSoftwareAPI.Services.monthlyInterest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MonthlyInterestController : ControllerBase
    {
        private readonly IMonthlyInterestService _interestService;
        private readonly ILogger<MonthlyInterestController> _logger;

        public MonthlyInterestController(IMonthlyInterestService interestService, ILogger<MonthlyInterestController> logger)
        {
            _interestService = interestService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetOverview()
        {
            try
            {
                return Ok(await _interestService.GetOverviewAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la configuración de intereses.");
                return StatusCode(500, new { message = "No se pudo obtener la configuración de intereses." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSetting([FromBody] CreateMonthlyInterestDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var created = await _interestService.CreateSettingAsync(dto);
                return CreatedAtAction(nameof(GetOverview), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear la configuración de intereses.");
                return StatusCode(500, new { message = "No se pudo guardar la configuración de intereses." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSetting(int id, [FromBody] UpdateMonthlyInterestDTO dto)
        {
            if (id <= 0) return BadRequest(new { message = "ID inválido." });
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                bool success = await _interestService.UpdateSettingAsync(id, dto);
                return success ? NoContent() : NotFound(new { message = "Configuración no encontrada." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar la configuración de intereses ID {Id}", id);
                return StatusCode(500, new { message = "No se pudo actualizar la configuración de intereses." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSetting(int id)
        {
            if (id <= 0) return BadRequest(new { message = "ID inválido." });
            try
            {
                bool success = await _interestService.DeleteSettingAsync(id);
                return success ? NoContent() : NotFound(new { message = "Configuración no encontrada." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar la configuración de intereses ID {Id}", id);
                return StatusCode(500, new { message = "No se pudo eliminar la configuración de intereses." });
            }
        }
    }
}
