using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<Address>> GetAddresses()
        {
            try
            {
                List<Address> addresses = null; //replace with service call

                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting addresses: {ex.Message}");
            }
        }
    }
}