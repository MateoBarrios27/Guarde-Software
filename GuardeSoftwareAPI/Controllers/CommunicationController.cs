using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Communication;
using GuardeSoftwareAPI.Services.communication;
// using System.Security.Claims; // To get the real user ID

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommunicationsController : ControllerBase
    {
        private readonly ICommunicationService _communicationService;
        private readonly ILogger<CommunicationsController> _logger;

        public CommunicationsController(ICommunicationService communicationService, ILogger<CommunicationsController> logger)
        {
            _communicationService = communicationService;
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

        [HttpPost]
        public async Task<IActionResult> CreateCommunication([FromBody] UpsertCommunicationRequest request)
        {
            try
            {
                // Get the user ID from the authenticated token
                // var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                // int userId = int.Parse(userIdString);

                // Using 1 as a placeholder for user ID
                int placeholderUserId = 1;

                var newCommunication = await _communicationService.CreateCommunicationAsync(request, placeholderUserId);

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
                // Get user ID from claims
                // var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                int placeholderUserId = 1; // Use placeholder
                
                var updatedComm = await _communicationService.UpdateCommunicationAsync(id, request, placeholderUserId);
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
                bool success = await _communicationService.DeleteCommunicationAsync(id);
                if (success)
                {
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
                return Ok(updatedComm); // Returns the updated DTO
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send communication with ID: {Id}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}