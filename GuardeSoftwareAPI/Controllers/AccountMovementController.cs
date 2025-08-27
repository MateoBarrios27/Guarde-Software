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

        [HttpGet("{id}")]
        public ActionResult<AccountMovement> GetAccountMovementById(int id)
        {
            try
            {
                AccountMovement accountMovement = new AccountMovement(); //replace with service call

                if (accountMovement == null)
                {
                    return NotFound($"Account movement id n°{id} not found ");
                }
                return Ok(accountMovement);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the account movement: {ex.Message}");
            }
        }    
    }
}