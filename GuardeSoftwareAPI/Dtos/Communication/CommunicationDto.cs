namespace GuardeSoftwareAPI.Dtos.Communication;

public class CommunicationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SendDate { get; set; } // yyyy-MM-dd
    public string? SendTime { get; set; } // HH:mm
    public string Channel { get; set; } = string.Empty; // "Email", "WhatsApp", "Email + WhatsApp"
    public List<string> Recipients { get; set; } = [];
    public string Status { get; set; } = string.Empty; // "Sent", "Scheduled", "Draft"
    public string CreationDate { get; set; } = string.Empty; // yyyy-MM-dd
    public int? SmtpConfigId { get; set; }
    public bool IsAccountStatement { get; set; }
}
    
    public class UpsertCommunicationRequest
{
    public int? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SendDate { get; set; }
    public string? SendTime { get; set; }
    public string Type { get; set; } = string.Empty; // 'schedule' | 'draft'
    public int? SmtpConfigId { get; set; }
    public List<string> Channels { get; set; } = [];
    public List<string> Recipients { get; set; } = [];
    public List<IFormFile>? Attachments { get; set; }
    public bool IsAccountStatement { get; set; }
}
