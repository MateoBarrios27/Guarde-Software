using System.Security.Claims;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Notification;
using GuardeSoftwareAPI.Services.notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public sealed class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly DaoUser _daoUser;

        public NotificationController(INotificationService notificationService, DaoUser daoUser)
        {
            _notificationService = notificationService;
            _daoUser = daoUser;
        }

        [HttpGet]
        public async Task<ActionResult<NotificationInboxDto>> GetInbox([FromQuery] int take = 50)
        {
            int? userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
                return Unauthorized();

            return Ok(await _notificationService.GetInboxAsync(userId.Value, take));
        }

        [HttpPut("{notificationId:long}/read")]
        public async Task<IActionResult> MarkAsRead(long notificationId)
        {
            int? userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
                return Unauthorized();

            return await _notificationService.MarkAsReadAsync(notificationId, userId.Value)
                ? NoContent()
                : NotFound();
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            int? userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
                return Unauthorized();

            int updated = await _notificationService.MarkAllAsReadAsync(userId.Value);
            return Ok(new { updated });
        }

        private async Task<int?> GetCurrentUserIdAsync()
        {
            string? businessUserId = User.FindFirst("businessUserId")?.Value;
            if (int.TryParse(businessUserId, out int parsedId) && parsedId > 0)
                return parsedId;

            string? identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(identityUserId))
                return null;

            DataTable table = await _daoUser.GetUserByIdentityUserId(identityUserId);
            return table.Rows.Count == 0 ? null : Convert.ToInt32(table.Rows[0]["user_id"]);
        }
    }
}
