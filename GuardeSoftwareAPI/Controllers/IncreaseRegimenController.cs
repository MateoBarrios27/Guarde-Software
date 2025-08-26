using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IncreaseRegimenController : ControllerBase
    {
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
    }
}
