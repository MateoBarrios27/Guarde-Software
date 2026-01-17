using GuardeSoftwareAPI.Dtos.BillingType;
using GuardeSoftwareAPI.Services.billingType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BillingTypeController : ControllerBase
    {
        private readonly IBillingTypeService _billingTypeService;
        private readonly ILogger<BillingTypeController> _logger;

        public BillingTypeController(IBillingTypeService billingTypeService, ILogger<BillingTypeController> logger)
        {
            _billingTypeService = billingTypeService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetBillingTypes()
        {
            try
            {
                var types = await _billingTypeService.GetBillingTypesAsync();
                return Ok(types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener los tipos de factura.");
                return StatusCode(500, new { message = "Error interno del servidor." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateBillingType([FromBody] CreateBillingTypeDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var newType = await _billingTypeService.CreateBillingTypeAsync(dto);
                return CreatedAtAction(nameof(GetBillingTypes), new { id = newType.Id }, newType); // Asumiendo que GetBillingTypes no toma ID
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear tipo de factura.");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBillingType(int id, [FromBody] UpdateBillingTypeDTO dto)
        {
            if (id <= 0) return BadRequest(new { message = "ID inválido." });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                bool success = await _billingTypeService.UpdateBillingTypeAsync(id, dto);
                if (!success)
                {
                    return NotFound(new { message = "Tipo de factura no encontrado." });
                }
                return NoContent(); // Éxito
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar tipo de factura ID {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBillingType(int id)
        {
            if (id <= 0) return BadRequest(new { message = "ID inválido." });
            try
            {
                bool success = await _billingTypeService.DeleteBillingTypeAsync(id);
                if (!success)
                {
                    return NotFound(new { message = "Tipo de factura no encontrado." });
                }
                return NoContent(); // Éxito
            }
            catch (InvalidOperationException ex) // Captura el error de "en uso"
            {
                _logger.LogWarning(ex, "Intento de eliminar tipo de factura en uso ID {Id}", id);
                return Conflict(new { message = ex.Message }); // 409 Conflict
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar tipo de factura ID {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}