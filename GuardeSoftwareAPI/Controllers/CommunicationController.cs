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

        public CommunicationsController(ICommunicationService communicationService)
        {
            _communicationService = communicationService;
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
    }
}