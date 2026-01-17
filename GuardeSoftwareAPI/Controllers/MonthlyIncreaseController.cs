using GuardeSoftwareAPI.Dtos.MonthlyIncrease;
using GuardeSoftwareAPI.Services.monthlyIncrease;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MonthlyIncreaseController : ControllerBase
    {
        private readonly IMonthlyIncreaseService _increaseService;
        private readonly ILogger<MonthlyIncreaseController> _logger;

        public MonthlyIncreaseController(IMonthlyIncreaseService increaseService, ILogger<MonthlyIncreaseController> logger)
        {
            _increaseService = increaseService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var types = await _increaseService.GetSettingsAsync();
                return Ok(types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuraciones de aumentos.");
                return StatusCode(500, new { message = "Error interno del servidor." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSetting([FromBody] CreateMonthlyIncreaseDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {        
                var newType = await _increaseService.CreateSettingAsync(dto);
                return CreatedAtAction(nameof(GetSettings), new { id = newType.Id }, newType);
            }
            catch (ArgumentException ex) // Error de validación
            {
                 return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex) 
            {
                return Conflict(new { message = ex.Message }); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración de aumento.");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSetting(int id, [FromBody] UpdateMonthlyIncreaseDTO dto)
        {
            if (id <= 0) return BadRequest(new { message = "ID inválido." });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                bool success = await _increaseService.UpdateSettingAsync(id, dto);
                if (!success)
                {
                    return NotFound(new { message = "Configuración no encontrada." });
                }
                return NoContent(); // Éxito
            }
            catch (ArgumentException ex) // Error de validación
            {
                 return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar configuración de aumento ID {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSetting(int id)
        {
            if (id <= 0) return BadRequest(new { message = "ID inválido." });
            try
            {
                bool success = await _increaseService.DeleteSettingAsync(id);
                if (!success)
                {
                    return NotFound(new { message = "Configuración no encontrada." });
                }
                return NoContent(); // Éxito
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración de aumento ID {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}