using GuardeSoftwareAPI.Dtos.CommunicationTemplate;
using GuardeSoftwareAPI.Services.communicationTemplate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    /// <summary>Comunicaciones → Plantillas. Observador: sólo lectura (ObserverReadOnlyMiddleware).</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CommunicationTemplatesController : ControllerBase
    {
        private readonly ICommunicationTemplateService _templateService;
        private readonly ILogger<CommunicationTemplatesController> _logger;

        public CommunicationTemplatesController(ICommunicationTemplateService templateService,
            ILogger<CommunicationTemplatesController> logger)
        {
            _templateService = templateService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                return Ok(await _templateService.GetAllAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener las plantillas de comunicados.");
                return StatusCode(500, new { message = "No se pudieron obtener las plantillas." });
            }
        }

        [HttpPost]
        [RequestSizeLimit(8_000_000)]
        public async Task<IActionResult> Create([FromBody] SaveCommunicationTemplateDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return await Run(async () => Ok(await _templateService.CreateAsync(dto)), "crear la plantilla");
        }

        [HttpPut("{id:int}")]
        [RequestSizeLimit(8_000_000)]
        public async Task<IActionResult> Update(int id, [FromBody] SaveCommunicationTemplateDTO dto)
        {
            if (id <= 0) return BadRequest(new { message = "ID inválido." });
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return await Run(async () =>
            {
                var updated = await _templateService.UpdateAsync(id, dto);
                return updated == null ? NotFound(new { message = "Plantilla no encontrada." }) : Ok(updated);
            }, "guardar la plantilla");
        }

        [HttpPut("order")]
        public async Task<IActionResult> Reorder([FromBody] ReorderCommunicationTemplatesDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return await Run(async () => Ok(await _templateService.ReorderAsync(dto.TemplateIds)), "ordenar las plantillas");
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return BadRequest(new { message = "ID inválido." });
            return await Run(async () => await _templateService.DeleteAsync(id)
                ? NoContent()
                : NotFound(new { message = "Plantilla no encontrada." }), "eliminar la plantilla");
        }

        private async Task<IActionResult> Run(Func<Task<IActionResult>> action, string what)
        {
            try
            {
                return await action();
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
                _logger.LogError(ex, "Error al {What}.", what);
                return StatusCode(500, new { message = $"No se pudo {what}." });
            }
        }
    }
}
