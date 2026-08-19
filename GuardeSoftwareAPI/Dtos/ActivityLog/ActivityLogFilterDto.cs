namespace GuardeSoftwareAPI.Dtos.ActivityLog;

public class ActivityLogFilterDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Area { get; set; }
    public string? Action { get; set; }
    public int? UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Search { get; set; }
}
