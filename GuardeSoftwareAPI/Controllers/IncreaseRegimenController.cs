using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.IncreaseRegimen;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IncreaseRegimenController : ControllerBase
    {
        private readonly IIncreaseRegimenService _increaseRegimenService;

        public IncreaseRegimenController(IIncreaseRegimenService increaseRegimenService)
        {
            _increaseRegimenService = increaseRegimenService;
        }

        [HttpGet]
        public ActionResult<List<IncreaseRegimen>> GetIncreaseRegimens()
        {
            try
            {
                List<IncreaseRegimen> increaseRegimens = null; //replace with service call

                return Ok(increaseRegimens);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting increase regimens: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<IncreaseRegimen> GetIncreaseRegimenById(int id)
        {
            try
            {
                IncreaseRegimen increaseRegimen = new IncreaseRegimen(); //replace with service call

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
