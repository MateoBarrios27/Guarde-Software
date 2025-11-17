using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Communication;
using GuardeSoftwareAPI.Services.communication;
using System.Text.Json; // Necesario para deserializar el DTO

// --- INTERFAZ (STUB) PARA EL SERVICIO DE ARCHIVOS ---
// Debes crear este servicio e inyectarlo
public interface IFileStorageService
{
    // Sube archivos al VPS y devuelve sus DTOs (con nombre y URL)
    Task<List<AttachmentDto>> UploadFilesAsync(IFormFileCollection files);
    // Borra archivos del VPS usando sus URLs (o nombres de archivo)
    Task DeleteFilesAsync(List<string> fileNamesOrUrls);
    Task DeleteFileAsync(string fileNameOrUrl);
}
// --- FIN INTERFAZ (STUB) ---


namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommunicationsController : ControllerBase
    {
        private readonly ICommunicationService _communicationService;
        private readonly IFileStorageService _fileStorageService; // --- AÑADIDO ---
        private readonly ILogger<CommunicationsController> _logger;

        public CommunicationsController(
            ICommunicationService communicationService, 
            ILogger<CommunicationsController> logger, 
            IFileStorageService fileStorageService // --- AÑADIDO ---
        )
        {
            _communicationService = communicationService;
            _logger = logger;
            _fileStorageService = fileStorageService; // --- AÑADIDO ---
        }

        [HttpGet]
        public async Task<IActionResult> GetCommunications()
        {
            var data = await _communicationService.GetCommunications();
            return Ok(data);
        }
        
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCommunicationById(int id)
        {
            try
            {
                var data = await _communicationService.GetCommunicationById(id);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // --- MÉTODO ACTUALIZADO ---
        [HttpPost]
        [Consumes("multipart/form-data")] // Recibe FormData
        public async Task<IActionResult> CreateCommunication(
            [FromForm] string comunicadoDto, 
            [FromForm] IFormFileCollection files)
        {
            try
            {
                // 1. Deserializar el string JSON
                var request = JsonSerializer.Deserialize<UpsertCommunicationRequest>(comunicadoDto, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (request == null) return BadRequest("El DTO del comunicado no es válido.");

                // 2. Subir los archivos nuevos al VPS
                var uploadedFiles = await _fileStorageService.UploadFilesAsync(files);
                
                int placeholderUserId = 1; // TODO: Cambiar por el ID de usuario real

                // 3. Llamar al servicio (ahora modificado)
                var newCommunication = await _communicationService.CreateCommunicationAsync(request, uploadedFiles, placeholderUserId);

                return CreatedAtAction(nameof(GetCommunicationById), new { id = newCommunication.Id }, newCommunication);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        
        // --- MÉTODO ACTUALIZADO ---
        [HttpPut("{id}")]
        [Consumes("multipart/form-data")] // Recibe FormData
        public async Task<IActionResult> UpdateCommunication(
            int id, 
            [FromForm] string comunicadoDto, 
            [FromForm] IFormFileCollection files)
        {
            try
            {
                var request = JsonSerializer.Deserialize<UpsertCommunicationRequest>(comunicadoDto, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (request == null || request.Id != id) return BadRequest("Datos inválidos.");

                int placeholderUserId = 1; // TODO: Cambiar por el ID de usuario real
                
                // 1. Subir solo los archivos *nuevos*
                var newFiles = await _fileStorageService.UploadFilesAsync(files);

                // 2. Llamar al servicio (ahora modificado)
                // El servicio se encargará de borrar los 'AttachmentsToRemove'
                var updatedComm = await _communicationService.UpdateCommunicationAsync(id, request, newFiles, placeholderUserId);
                return Ok(updatedComm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update communication with ID: {Id}", id);
                return StatusCode(500, new { message = ex.Message, innerException = ex.InnerException?.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCommunication(int id)
        {
            // El 'CommunicationService' ahora se encargará de borrar 
            // los archivos del VPS *antes* de borrar el registro de la DB.
            try
            {
                bool success = await _communicationService.DeleteCommunicationAsync(id);
                if (success)
                {
                    return NoContent();
                }
                else
                {
                    return NotFound(new { message = "Communication not found." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete communication with ID: {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("{id}/send")]
        public async Task<IActionResult> SendCommunicationNow(int id)
        {
            // Este endpoint está perfecto. Llama al servicio,
            // y el servicio encola el trabajo de Quartz.
            try
            {
                var updatedComm = await _communicationService.SendDraftNowAsync(id);
                return Ok(updatedComm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send communication with ID: {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // --- ENDPOINT NUEVO ---
        [HttpPost("{id}/retry")]
        public async Task<IActionResult> RetryFailedSends(int id, [FromBody] RetryRequestDto retryRequest)
        {
            if (string.IsNullOrEmpty(retryRequest.MailServerId))
            {
                return BadRequest("Debe proveer un 'mailServerId'.");
            }

            try
            {
                // Llama al nuevo método en el servicio
                var updatedComm = await _communicationService.RetryFailedSendsAsync(id, retryRequest.MailServerId);
                return Ok(updatedComm);
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Failed to retry communication with ID: {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // --- ENDPOINT NUEVO ---
        [HttpDelete("{id}/attachments/{fileName}")]
        public async Task<IActionResult> DeleteAttachment(int id, string fileName)
        {
            try
            {
                // Llama al nuevo método en el servicio
                await _communicationService.DeleteAttachmentAsync(id, fileName);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete attachment {FileName} from {Id}", fileName, id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("client/{clientId}")]
        public async Task<IActionResult> GetCommunicationsByClientId(int clientId)
        {
            if (clientId <= 0)
            {
                return BadRequest(new { message = "El ID del cliente es inválido." });
            }
            try
            {
                var communications = await _communicationService.GetCommunicationsByClientIdAsync(clientId);
                return Ok(communications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el historial de comunicaciones para el cliente ID {ClientId}", clientId);
                return StatusCode(500, new { message = "Error interno al obtener comunicaciones." });
            }
        }
    }
}