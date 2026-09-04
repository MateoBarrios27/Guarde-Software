using GuardeSoftwareAPI.Dtos.MassCommunicationRecipient;
using GuardeSoftwareAPI.Services.massCommunicationRecipient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MassCommunicationRecipientsController : ControllerBase
    {
        private readonly IMassCommunicationRecipientService _service;
        private readonly ILogger<MassCommunicationRecipientsController> _logger;

        public MassCommunicationRecipientsController(
            IMassCommunicationRecipientService service,
            ILogger<MassCommunicationRecipientsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<MassCommunicationRecipientDto>>> GetAll()
        {
            try
            {
                return Ok(await _service.GetAllAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener los receptores de comunicados masivos.");
                return StatusCode(500, new { message = "No se pudieron obtener los receptores." });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<MassCommunicationRecipientDto>> GetById(int id)
        {
            try
            {
                var recipient = await _service.GetByIdAsync(id);
                if (recipient is null)
                {
                    return NotFound();
                }

                return Ok(recipient);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el receptor {RecipientId}.", id);
                return StatusCode(500, new { message = "No se pudo obtener el receptor." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<MassCommunicationRecipientDto>> Create([FromBody] UpsertMassCommunicationRecipientDto dto)
        {
            try
            {
                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear un receptor de comunicados masivos.");
                return StatusCode(500, new { message = "No se pudo crear el receptor." });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<MassCommunicationRecipientDto>> Update(
            int id,
            [FromBody] UpsertMassCommunicationRecipientDto dto)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                if (updated is null)
                {
                    return NotFound();
                }

                return Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar el receptor {RecipientId}.", id);
                return StatusCode(500, new { message = "No se pudo actualizar el receptor." });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                bool deleted = await _service.DeleteAsync(id);
                return deleted ? NoContent() : NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el receptor {RecipientId}.", id);
                return StatusCode(500, new { message = "No se pudo eliminar el receptor." });
            }
        }

        [HttpPost("import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<MassCommunicationRecipientImportResultDto>> Import(
            [FromForm] MassCommunicationRecipientImportRequest request)
        {
            try
            {
                return Ok(await _service.ImportAsync(request));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al importar receptores de comunicados masivos.");
                return StatusCode(500, new { message = "No se pudieron importar los receptores." });
            }
        }
    }
}
