using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.locker;
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Locker;
using System.Threading.Tasks;

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
        public async Task<ActionResult<List<Locker>>> GetLockers()
        {
            try
            {
                List<Locker> lockers = await _lockerService.GetLockersList();
                return Ok(lockers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting lockers: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Locker>> GetLockersById(int id)
        {
            try
            {
                List<Locker> locker = await _lockerService.GetLockerListById(id);

                if (locker == null)
                {
                    return NotFound($"locker id n�{id} not found ");
                }
                return Ok(locker);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the locker: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLocker(int id)
        {
            bool deleted = await _lockerService.DeleteLocker(id);

            if (deleted)
                return Ok(new { message = "Locker deleted successfully." });
            else
                return NotFound(new { message = "No Locker found with the given ID." });
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLocker(int id, [FromBody] UpdateLockerDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Invalid locker ID." });

            try
            {
                bool updated = await _lockerService.UpdateLocker(id,dto);

                if (updated)
                    return Ok(new { message = "Locker updated successfully." });
                else
                    return NotFound(new { message = $"No locker found with ID {id}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating locker: {ex.Message}");
            }
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateLockerStatus(int id, [FromBody] UpdateLockerStatusDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Invalid locker ID." });

            try
            {
                bool updated = await _lockerService.UpdateLockerStatus(id,dto);

                if (updated)
                    return Ok(new { message = "Locker updated successfully." });
                else
                    return NotFound(new { message = $"No locker found with ID {id}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating locker: {ex.Message}");
            }
        }
    }
}