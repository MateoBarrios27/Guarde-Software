using GuardeSoftwareAPI.Services.notification;
using Quartz;

[DisallowConcurrentExecution]
public sealed class ArrearsNotificationsJob(INotificationService notifications) : IJob
{
    public Task Execute(IJobExecutionContext context)
        => notifications.SyncArrearsNotificationsAsync(DateTime.UtcNow.AddHours(-3).Date);
}
