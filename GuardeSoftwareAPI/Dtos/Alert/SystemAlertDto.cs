namespace GuardeSoftwareAPI.Dtos.Alert
{
    public class SystemAlertDto
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Severidad del cartel: "warning" | "danger" | "info" | "maintenance"
        /// </summary>
        public string Severity { get; set; } = "warning";
        public string SenderName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
