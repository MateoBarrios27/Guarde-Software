using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Communication;
using GuardeSoftwareAPI.Services.communication;
using GuardeSoftwareAPI.Services.activityLog;
using GuardeSoftwareAPI.Entities;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CommunicationsController : ControllerBase
    {
        private readonly ICommunicationService _communicationService;
        private readonly IActivityLogService _activityLogService;
        private readonly ILogger<CommunicationsController> _logger;

        public CommunicationsController(ICommunicationService communicationService, IActivityLogService activityLogService, ILogger<CommunicationsController> logger)
        {
            _communicationService = communicationService;
            _activityLogService = activityLogService;
            _logger = logger;
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

        [HttpGet("dispatch/{dispatchId}/content")]
        public async Task<IActionResult> GetDispatchContent(int dispatchId)
        {
            var content = await _communicationService.GetDispatchContentAsync(dispatchId);
            if (content == null)
                return NotFound(new { message = "No hay contenido guardado para este envío." });
            return Ok(new { content });
        }

        [HttpPost]
        public async Task<IActionResult> CreateCommunication([FromForm] UpsertCommunicationRequest request)
        {
            try
            {
                int userId = await GetCurrentUserIdAsync();
                var newCommunication = await _communicationService.CreateCommunicationAsync(request, userId);

                await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
                {
                    Action = "CREATE",
                    TableName = "communications",
                    RecordId = newCommunication.Id,
                    NewValue = JsonSerializer.Serialize(CreateCommunicationSnapshot(newCommunication))
                });

                // Return a 201 Created status with the new object
                return CreatedAtAction(nameof(GetCommunicationById), new { id = newCommunication.Id }, newCommunication);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCommunication(int id, [FromBody] UpsertCommunicationRequest request)
        {
            try
            {
                int userId = await GetCurrentUserIdAsync();
                var previousComm = await _communicationService.GetCommunicationById(id);
                var updatedComm = await _communicationService.UpdateCommunicationAsync(id, request, userId);

                await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
                {
                    Action = "UPDATE",
                    TableName = "communications",
                    RecordId = id,
                    OldValue = JsonSerializer.Serialize(CreateCommunicationSnapshot(previousComm)),
                    NewValue = JsonSerializer.Serialize(CreateCommunicationSnapshot(updatedComm))
                });
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
            try
            {
                var previousComm = await _communicationService.GetCommunicationById(id);
                bool success = await _communicationService.DeleteCommunicationAsync(id);
                if (success)
                {
                    await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
                    {
                        Action = "DELETE",
                        TableName = "communications",
                        RecordId = id,
                        OldValue = JsonSerializer.Serialize(CreateCommunicationSnapshot(previousComm)),
                        NewValue = JsonSerializer.Serialize(new { Deleted = true })
                    });
                    return NoContent(); // 204 No Content is standard for successful delete
                }
                else
                {
                    return NotFound(new { message = "Communication not found or already deleted." });
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
            try
            {
                var updatedComm = await _communicationService.SendDraftNowAsync(id);
                await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
                {
                    Action = "UPDATE",
                    TableName = "communications",
                    RecordId = id,
                    NewValue = JsonSerializer.Serialize(new { Operation = "SEND_NOW", updatedComm.Status, updatedComm.SendDate, updatedComm.SendTime })
                });
                return Ok(updatedComm); // Returns the updated DTO
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send communication with ID: {Id}", id);
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

        [HttpPost("{id}/retry")]
        public async Task<IActionResult> RetryFailedCommunication(int id)
        {
            try
            {
                var updatedComm = await _communicationService.RetrySelectedFailedCommunicationAsync(
                    id,
                    new List<int>(),
                    null);
                await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
                {
                    Action = "UPDATE",
                    TableName = "communications",
                    RecordId = id,
                    NewValue = JsonSerializer.Serialize(new { Operation = "RETRY", updatedComm.Status, updatedComm.SendDate, updatedComm.SendTime })
                });
                return Ok(updatedComm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reintentando comunicado {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("{id}/retry-selected")]
        public async Task<IActionResult> RetrySelectedFailedCommunication(int id, [FromBody] RetryCommunicationRequest request)
        {
            try
            {
                var updatedComm = await _communicationService.RetrySelectedFailedCommunicationAsync(
                    id,
                    request?.SelectedClientIds ?? new List<int>(),
                    request?.SelectedExternalRecipientIds ?? new List<int>());
                await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
                {
                    Action = "UPDATE",
                    TableName = "communications",
                    RecordId = id,
                    NewValue = JsonSerializer.Serialize(new
                    {
                        Operation = "RETRY_SELECTED",
                        updatedComm.Status,
                        updatedComm.SendDate,
                        updatedComm.SendTime,
                        SelectedCount = (request?.SelectedClientIds?.Count ?? 0)
                            + (request?.SelectedExternalRecipientIds?.Count ?? 0)
                    })
                });
                return Ok(updatedComm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reintentando seleccionados del comunicado {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        private async Task<int> GetCurrentUserIdAsync()
        {
            int? userId = await _activityLogService.GetCurrentUserIdAsync();
            if (!userId.HasValue)
                throw new UnauthorizedAccessException("No se pudo identificar al usuario autenticado.");

            return userId.Value;
        }

        private static object CreateCommunicationSnapshot(CommunicationDto communication)
        {
            return new
            {
                communication.Id,
                communication.Title,
                communication.Status,
                communication.Channel,
                communication.SmtpConfigId,
                communication.IsAccountStatement,
                communication.IsNextMonthStatement,
                communication.SendToAllEmails,
                RecipientCount = (communication.Recipients?.Count ?? 0)
                    + (communication.ExternalRecipients?.Count ?? 0),
                ExternalRecipientCount = communication.ExternalRecipients?.Count ?? 0,
                AttachmentCount = communication.Dispatches?.Count ?? 0
            };
        }

        [HttpGet("recipients-list")]
        public async Task<IActionResult> GetClientsForSelector()
        {
            try
            {
                var clients = await _communicationService.GetClientsForSelectorAsync(); 
                return Ok(clients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo lista de destinatarios");
                return StatusCode(500, new { message = "Error interno" });
            }
        }
    }
}
