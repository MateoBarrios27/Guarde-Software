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
    }
}