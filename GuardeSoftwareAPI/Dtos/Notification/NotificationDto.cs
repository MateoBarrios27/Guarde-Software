namespace GuardeSoftwareAPI.Dtos.Notification
{
    public sealed class NotificationDto
    {
        public long Id { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    public sealed class NotificationInboxDto
    {
        public List<NotificationDto> Items { get; set; } = [];
        public int UnreadCount { get; set; }
    }
}
