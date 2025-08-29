using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.clientIncreaseRegimen;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientIncreaseRegimenController : ControllerBase
    {
        private readonly IClientIncreaseRegimenService _clientIncreaseRegimenService;

        public ClientIncreaseRegimenController(IClientIncreaseRegimenService clientIncreaseRegimenService)
        {
            _clientIncreaseRegimenService = clientIncreaseRegimenService;
        }
        
        [HttpGet]
        public ActionResult<List<ClientIncreaseRegimen>> GetClientIncreaseRegimens()
        {
            try
            {
                List<ClientIncreaseRegimen> clientIncreaseRegimens = _clientIncreaseRegimenService.GetClientIncreaseRegimensList();

                return Ok(clientIncreaseRegimens);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting client increase regimens: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<ClientIncreaseRegimen> GetClientIncreaseRegimenById(int id)
        {
            try
            {
                List<ClientIncreaseRegimen> clientIncreaseRegimen = _clientIncreaseRegimenService.GetClientIncreaseRegimensListByClientId(id);

                if (clientIncreaseRegimen == null)
                {
                    return NotFound($"ClientIncreaseRegimen id n°{id} not found ");
                }
                return Ok(clientIncreaseRegimen);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the client increase regimen: {ex.Message}");
            }
        }    
    }
}