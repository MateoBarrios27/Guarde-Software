using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.LockerType;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.lockerType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LockerTypeController : ControllerBase
    {
        private readonly ILockerTypeService _lockerTypeService;

        public LockerTypeController(ILockerTypeService lockerTypeService)
        {
            _lockerTypeService = lockerTypeService;
        }

        [HttpGet]
        public async Task<ActionResult<List<LockerType>>> GetLockerTypes()
        {
            try
            {
                List<LockerType> lockerTypes = await _lockerTypeService.GetLockerTypesList();

                return Ok(lockerTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting locker types: {ex.Message}");
            }
        }

        [HttpGet("{id}", Name = "GetLockerTypeById")]
        public async Task<ActionResult<LockerType>> GetLockerTypeById(int id)
        {
            try
            {
                List<LockerType> lockerTypes = await _lockerTypeService.GetLockerTypeListById(id);

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
        public async Task<ActionResult> CreateLockerType([FromBody] CreateLockerTypeDto lockerTypeToCreate)
        {
            if (lockerTypeToCreate == null)
            {
                return BadRequest("Locker type data is null.");
            }

            LockerType lockerType = new()
            {
                Name = lockerTypeToCreate.Name,
                Amount = lockerTypeToCreate.Amount,
                M3 = lockerTypeToCreate.M3
            };
            
            try
            {
                lockerType = await _lockerTypeService.CreateLockerType(lockerType);
                return CreatedAtAction(nameof(GetLockerTypeById), new { id = lockerType.Id }, lockerType);
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