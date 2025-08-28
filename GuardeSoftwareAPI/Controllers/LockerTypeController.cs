using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.lockerType;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LockerTypeController : ControllerBase
    {
        private readonly ILockerTypeService _lockerTypeService;

        public LockerTypeController(ILockerTypeService lockerTypeService)
        {
            _lockerTypeService = lockerTypeService;
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