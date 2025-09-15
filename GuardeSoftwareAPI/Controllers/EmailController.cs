using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.email; 
using Microsoft.AspNetCore.Mvc;

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
        public ActionResult<List<Email>> GetEmails()
        {
            try
            {
                List<Email> emails = _emailService.GetEmailsList();

                return Ok(emails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting emails: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<Email> GetEmailByClientId(int id)
        {
            try
            {
                List<Email> email = _emailService.GetEmailListByClientId(id);

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
        public IActionResult DeleteEmail(int id)
        {
            bool deleted = _emailService.DeleteEmail(id);

            if (deleted)
                return Ok(new { message = "Email deleted successfully." });
            else
                return NotFound(new { message = "No Email found with the given ID." });
        }
    }
}