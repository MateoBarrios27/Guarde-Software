using System.Threading.Tasks;
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
        public async Task<ActionResult<List<AccountMovement>>> GetAccountMovements()
        {
            try
            {
                List<AccountMovement> accountMovements = await _accountMovementService.GetAccountMovementList();

                return Ok(accountMovements);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting account movements: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AccountMovement>> GetAccountMovementById(int id)
        {
            try
            {
                List<AccountMovement> accountMovement = await _accountMovementService.GetAccountMovementListByRentalId(id);

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