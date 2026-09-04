using Microsoft.AspNetCore.Http;

namespace GuardeSoftwareAPI.Dtos.MassCommunicationRecipient;

public class MassCommunicationRecipientImportRequest
{
    public IFormFile? File { get; set; }
    public string? Type { get; set; }
    public bool ReactivateInactive { get; set; }
    public bool DryRun { get; set; } = true;
}

public class MassCommunicationRecipientImportIssueDto
{
    public int RowNumber { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class MassCommunicationRecipientImportResultDto
{
    public bool DryRun { get; set; }
    public string Type { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int NewCount { get; set; }
    public int ExistingActiveCount { get; set; }
    public int ExistingInactiveCount { get; set; }
    public int ReactivatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedInactiveCount { get; set; }
    public int DuplicateCount { get; set; }
    public int InvalidCount { get; set; }
    public int MissingEmailCount { get; set; }
    public int ImportedCount { get; set; }
    public bool HasMoreIssues { get; set; }
    public List<MassCommunicationRecipientImportIssueDto> Issues { get; set; } = [];
}
