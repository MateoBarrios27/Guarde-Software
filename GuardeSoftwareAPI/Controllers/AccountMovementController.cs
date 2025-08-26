using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountMovementController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<AccountMovement>> GetAccountMovements()
        {
            try
            {
                List<AccountMovement> accountMovements = null; //replace with service call

                return Ok(accountMovements);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting account movements: {ex.Message}");
            }
        }
    }
}