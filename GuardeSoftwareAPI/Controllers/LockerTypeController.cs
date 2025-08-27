using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.Locker;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LockerTypeController : ControllerBase
    {
        private readonly ILockerService _lockerService;

        public LockerTypeController(ILockerService lockerService)
        {
            _lockerService = lockerService;
        }

        [HttpGet]
        public ActionResult<List<LockerType>> GetLockerTypes()
        {
            try
            {
                List<LockerType> lockerTypes = null; //replace with service call

                return Ok(lockerTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting locker types: {ex.Message}");
            }
        }
    }
}