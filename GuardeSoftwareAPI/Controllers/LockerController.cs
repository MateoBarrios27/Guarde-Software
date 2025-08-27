using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.Locker;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LockerController : ControllerBase
    {
        private readonly ILockerService _lockerService;

        public LockerController(ILockerService lockerService)
        {
            _lockerService = lockerService;
        }
        
        [HttpGet]
        public ActionResult<List<Locker>> GetLockers()
        {
            try
            {
                List<Locker> lockers = null; //replace with service call

                return Ok(lockers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting lockers: {ex.Message}");
            }
        }
    }
}