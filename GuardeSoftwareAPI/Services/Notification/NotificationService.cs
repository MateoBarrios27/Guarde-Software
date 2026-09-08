using System.Data;
using System.Globalization;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Notification;
using GuardeSoftwareAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GuardeSoftwareAPI.Services.notification
{
    public sealed class NotificationService : INotificationService
    {
        private readonly DaoNotification _dao;
        private readonly IHubContext<AlertHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            DaoNotification dao,
            IHubContext<AlertHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _dao = dao;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<NotificationInboxDto> GetInboxAsync(int userId, int take = 50)
        {
            await SyncOperationalNotificationsAsync(DateTime.Today);
            DataTable table = await _dao.GetInboxAsync(userId, take);
            int unreadCount = await _dao.GetUnreadCountAsync(userId);
            var items = new List<NotificationDto>(table.Rows.Count);

            foreach (DataRow row in table.Rows)
            {
                items.Add(new NotificationDto
                {
                    Id = Convert.ToInt64(row["notification_id"]),
                    SourceType = row["source_type"]?.ToString() ?? string.Empty,
                    Severity = row["severity"]?.ToString() ?? "info",
                    Title = row["title"]?.ToString() ?? string.Empty,
                    Message = row["message"]?.ToString() ?? string.Empty,
                    ActionUrl = row["action_url"] == DBNull.Value ? null : row["action_url"]?.ToString(),
                    CreatedAt = Convert.ToDateTime(row["created_at"]),
                    IsRead = Convert.ToBoolean(row["is_read"])
                });
            }

            return new NotificationInboxDto
            {
                Items = items,
                UnreadCount = unreadCount
            };
        }

        public async Task<bool> MarkAsReadAsync(long notificationId, int userId)
            => await _dao.MarkAsReadAsync(notificationId, userId) > 0;

        public Task<int> MarkAllAsReadAsync(int userId)
            => _dao.MarkAllAsReadAsync(userId);

        public async Task<long> CreateEventAsync(
            string sourceType,
            string severity,
            string title,
            string message,
            string? actionUrl = null,
            int? targetUserId = null,
            int? createdByUserId = null,
            string? notificationKey = null,
            DateTime? expiresAt = null)
        {
            try
            {
                long id = await _dao.CreateAsync(
                    sourceType,
                    severity,
                    title,
                    message,
                    actionUrl,
                    targetUserId,
                    createdByUserId,
                    notificationKey,
                    expiresAt);
                await _hubContext.Clients.All.SendAsync("NotificationsChanged");
                return id;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627 && notificationKey != null)
            {
                _logger.LogInformation("La notificación idempotente {NotificationKey} ya existía.", notificationKey);
                return 0;
            }
        }

        private async Task SyncOperationalNotificationsAsync(DateTime today)
        {
            string monthKey = today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            string missingIncreaseKey = $"monthly-increase-missing:{monthKey}";

            if (await _dao.HasMonthlyIncreaseAsync(today))
            {
                await _dao.ResolveByKeyAsync(missingIncreaseKey);
            }
            else
            {
                string monthName = CultureInfo.GetCultureInfo("es-AR").DateTimeFormat.GetMonthName(today.Month);
                await _dao.UpsertOperationalAsync(
                    missingIncreaseKey,
                    "monthly_increase_missing",
                    "danger",
                    "Falta configurar el aumento mensual",
                    $"{char.ToUpper(monthName[0])}{monthName[1..]} {today.Year} no tiene un porcentaje de aumento configurado.",
                    "/settings?section=aumentos");
            }

            // Desde el día 25 se anticipan los clientes impagos cuyo ancla cae el mes siguiente.
            // La sincronización también resuelve avisos cuando el pago o la asignación ya se hicieron.
            DateTime nextMonth = new(today.AddMonths(1).Year, today.AddMonths(1).Month, 1);
            string nextMonthLabel = nextMonth.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-AR"));
            await _dao.SyncClientIncreaseNotificationsAsync(today, nextMonthLabel);
        }
    }
}
