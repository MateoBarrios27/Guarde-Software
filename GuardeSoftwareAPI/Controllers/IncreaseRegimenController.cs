using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.increaseRegimen;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IncreaseRegimenController : ControllerBase
    {
        private readonly IIncreaseRegimenService _increaseRegimenService;

        public IncreaseRegimenController(IIncreaseRegimenService increaseRegimenService)
        {
            _increaseRegimenService = increaseRegimenService;
        }

        [HttpGet]
        public async Task<ActionResult<List<IncreaseRegimen>>> GetIncreaseRegimens()
        {
            try
            {
                List<IncreaseRegimen> increaseRegimens = await _increaseRegimenService.GetIncreaseRegimensList();

                return Ok(increaseRegimens);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting increase regimens: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<IncreaseRegimen>> GetIncreaseRegimenById(int id)
        {
            try
            {
                List<IncreaseRegimen> increaseRegimen = await _increaseRegimenService.GetIncreaseRegimenListById(id);

                if (increaseRegimen == null)
                {
                    return NotFound($"Increase regimen id n°{id} not found ");
                }
                return Ok(increaseRegimen);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the increase regimen: {ex.Message}");
            }
        }    
    }
}
