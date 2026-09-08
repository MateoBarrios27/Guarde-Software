using GuardeSoftwareAPI.Dtos.Notification;

namespace GuardeSoftwareAPI.Services.notification
{
    public interface INotificationService
    {
        Task<NotificationInboxDto> GetInboxAsync(int userId, int take = 50);
        Task<bool> MarkAsReadAsync(long notificationId, int userId);
        Task<int> MarkAllAsReadAsync(int userId);
        Task<long> CreateEventAsync(
            string sourceType,
            string severity,
            string title,
            string message,
            string? actionUrl = null,
            int? targetUserId = null,
            int? createdByUserId = null,
            string? notificationKey = null,
            DateTime? expiresAt = null);
    }
}
