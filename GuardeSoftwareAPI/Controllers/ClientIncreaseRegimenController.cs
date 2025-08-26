using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientIncreaseRegimenController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<ClientIncreaseRegimen>> GetClientIncreaseRegimens()
        {
            try
            {
                List<ClientIncreaseRegimen> clientIncreaseRegimens = null; //replace with service call

                return Ok(clientIncreaseRegimens);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting client increase regimens: {ex.Message}");
            }
        }
    }
}