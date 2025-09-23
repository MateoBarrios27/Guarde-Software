using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.activityLog;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivityLogController : ControllerBase
    {
        private readonly IActivityLogService _activityLogService;

        public ActivityLogController(IActivityLogService activityLogService)
        {
            _activityLogService = activityLogService;
        }

        [HttpGet]
        public async Task<ActionResult<List<ActivityLog>>> GetActivityLogs()
        {
            try
            {
                List<ActivityLog> activityLogs = await _activityLogService.GetActivityLogList();

                return Ok(activityLogs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting activity logs: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ActivityLog>> GetActivityLogPerId(int id)
        {
            try
            {
                List<ActivityLog> activityLog = await _activityLogService.GetActivityLoglistByUserId(id);

                if (activityLog == null)
                {
                    return NotFound($"Activity log id n°{id} not found ");
                }
                return Ok(activityLog);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the activity log: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteActivityLog(int id)
        {
            try
            {
                if (await _activityLogService.DeleteActivityLog(id))
                    return Ok($"Activity log id n°{id} deleted successfully.");
                else
                    return NotFound($"Activity log id n°{id} not found.");
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