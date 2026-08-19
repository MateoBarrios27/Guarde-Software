using GuardeSoftwareAPI.Dtos.ActivityLog;
using GuardeSoftwareAPI.Dtos.Common;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.activityLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ActivityLogController : ControllerBase
    {
        private readonly IActivityLogService _activityLogService;

        public ActivityLogController(IActivityLogService activityLogService)
        {
            _activityLogService = activityLogService;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResultDto<ActivityLog>>> GetActivityLogs([FromQuery] ActivityLogFilterDto filter)
        {
            if (!await _activityLogService.IsCurrentUserAdminAsync())
                return Forbid();

            try
            {
                return Ok(await _activityLogService.GetActivityLogPageAsync(filter));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting activity logs: {ex.Message}");
            }
        }

        [HttpGet("users")]
        public async Task<ActionResult<List<ActivityLogUserDto>>> GetActivityLogUsers()
        {
            if (!await _activityLogService.IsCurrentUserAdminAsync())
                return Forbid();

            try
            {
                return Ok(await _activityLogService.GetActivityLogUsersAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting activity log users: {ex.Message}");
            }
        }

        // Se conserva la consulta histórica por usuario para compatibilidad.
        [HttpGet("{id:int}")]
        [HttpGet("by-user/{id:int}")]
        public async Task<ActionResult<List<ActivityLog>>> GetActivityLogPerUser(int id)
        {
            if (!await _activityLogService.IsCurrentUserAdminAsync())
                return Forbid();

            try
            {
                return Ok(await _activityLogService.GetActivityLoglistByUserId(id));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the activity log: {ex.Message}");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> DeleteActivityLog(int id)
        {
            if (!await _activityLogService.IsCurrentUserAdminAsync())
                return Forbid();

            try
            {
                if (await _activityLogService.DeleteActivityLog(id))
                    return Ok(new { message = $"Activity log id n°{id} deleted successfully." });

                return NotFound(new { message = $"Activity log id n°{id} not found." });
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting the activity log: {ex.Message}");
            }
        }
    }
}
