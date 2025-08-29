using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.accountMovement;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountMovementController : ControllerBase
    {
        readonly IAccountMovementService _accountMovementService;

        public AccountMovementController(IAccountMovementService accountMovementService)
        {
            _accountMovementService = accountMovementService;
        }

        [HttpGet]
        public ActionResult<List<AccountMovement>> GetAccountMovements()
        {
            try
            {
                List<AccountMovement> accountMovements = _accountMovementService.GetAccountMovementList();

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
                List<AccountMovement> accountMovement = _accountMovementService.GetAccountMovementListByRentalId(id);

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