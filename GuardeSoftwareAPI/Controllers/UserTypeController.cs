using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserTypeController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<UserType>> GetUserTypes()
        {
            try
            {
                List<UserType> userTypes = null; //replace with service call

                return Ok(userTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting user types: {ex.Message}");
            }
        }
    }
}
