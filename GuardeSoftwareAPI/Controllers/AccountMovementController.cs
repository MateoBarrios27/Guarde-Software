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

        [HttpGet("client/{clientId}")]
        public async Task<IActionResult> GetMovementsByClientId(int clientId)
        {
            try
            {
                if (clientId <= 0)
                {
                    return BadRequest("El ID del cliente es inválido.");
                }

                var movements = await _accountMovementService.GetAccountMovementListByClientIdAsync(clientId);
                return Ok(movements);
            }
            catch (Exception ex)
            {
                // Log the exception (ex)
                return StatusCode(500, "Error al obtener los movimientos del cliente.");
            }
        }

        /// <summary>
        /// Elimina un movimiento de cuenta específico por su ID.
        /// </summary>
        [HttpDelete("{movementId}")]
        public async Task<IActionResult> DeleteMovement(int movementId)
        {
            try
            {
                if (movementId <= 0)
                {
                    return BadRequest("El ID de movimiento es inválido.");
                }

                var success = await _accountMovementService.DeleteAccountMovementAsync(movementId);

                if (!success)
                {
                    return NotFound("No se encontró el movimiento o no se pudo eliminar (ej. está asociado a un pago).");
                }

                return NoContent(); // Éxito (Sin contenido)
            }
            catch (InvalidOperationException ex) // Captura la regla de negocio
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Log the exception (ex)
                return StatusCode(500, "Error al eliminar el movimiento.");
            }
        }
    }
}