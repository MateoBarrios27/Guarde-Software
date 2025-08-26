using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<Email>> GetEmails()
        {
            try
            {
                List<Email> emails = null; //replace with service call

                return Ok(emails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting emails: {ex.Message}");
            }
        }
    }
}