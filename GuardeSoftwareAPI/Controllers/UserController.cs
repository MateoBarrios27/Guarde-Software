using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<User>> GetUsers()
        {
            try
            {
                List<User> users = null; //replace with service call

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting users: {ex.Message}");
            }
        }
    }
}