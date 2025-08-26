using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhoneController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<Phone>> GetPhones()
        {
            try
            {
                List<Phone> phones = null; //replace with service call

                return Ok(phones);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting phones: {ex.Message}");
            }
        }
    }
}