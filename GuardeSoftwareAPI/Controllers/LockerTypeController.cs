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
                List<LockerType> lockerTypes = _lockerTypeService.GetLockerTypesList();

                return Ok(lockerTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting locker types: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<LockerType> GetLockerTypeById(int id)
        {
            try
            {
                List<LockerType> lockerTypes = _lockerTypeService.GetLockerTypeListById(id);

                if (lockerTypes == null)
                {
                    return NotFound($"locker type id n�{id} not found ");
                }
                return Ok(lockerTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the locker type: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<ActionResult> CreateLockerType([FromBody] LockerType lockerType)
        {
            try
            {
                if (lockerType == null)
                {
                    return BadRequest("Locker type data is null.");
                }

                await _lockerTypeService.CreateLockerType(lockerType);
                return Ok("Locker type created successfully.");
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating locker type: {ex.Message}");
            }
        }
    }
}