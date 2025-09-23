using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.email; 
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Email;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        readonly IEmailService _emailService;

        public EmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<ActionResult<List<Email>>> GetEmails()
        {
            try
            {
                List<Email> emails = await _emailService.GetEmailsList();

                return Ok(emails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting emails: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Email>> GetEmailByClientId(int id)
        {
            try
            {
                List<Email> email = await _emailService.GetEmailListByClientId(id);

                if (email == null)
                {
                    return NotFound($"Email with client id n°{id} not found ");
                }
                return Ok(email);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the email: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmail(int id)
        {
            bool deleted = await _emailService.DeleteEmail(id);

            if (deleted)
                return Ok(new { message = "Email deleted successfully." });
            else
                return NotFound(new { message = "No Email found with the given ID." });
        }

        [HttpPut("{clientId}")]
        public async Task<ActionResult> UpdateEmail(int clientId, [FromBody] UpdateEmailDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest("Email data is required.");

                bool updated = await _emailService.UpdateEmail(clientId, dto);

                if (!updated)
                    return NotFound($"No Email found with Id {dto.Id} for client {clientId}.");

                return Ok("Email updated successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating Email: {ex.Message}");
            }
        }
    }
}