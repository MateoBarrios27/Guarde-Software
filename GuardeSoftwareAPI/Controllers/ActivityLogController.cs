using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.ActivityLog;
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
        public ActionResult<List<ActivityLog>> GetActivityLogs()
        {
            try
            {
                List<ActivityLog> activityLogs = null; //replace with service call

                return Ok(activityLogs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting activity logs: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<ActivityLog> GetActivityLogPerId(int id)
        {
            try
            {
                ActivityLog activityLog = new ActivityLog(); //replace with service call

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
    }
}