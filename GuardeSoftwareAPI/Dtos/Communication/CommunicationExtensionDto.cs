namespace GuardeSoftwareAPI.Dtos.Communication;

public static class CommunicationExtensionModes
{
    public const string NeverAttempted = "never-attempted";
    public const string WithoutSuccessfulDelivery = "without-success";
}

public class ExtendCommunicationRequest
{
    public string RecipientType { get; set; } = "Inmobiliaria";
    public string Mode { get; set; } = CommunicationExtensionModes.NeverAttempted;
}

public class CommunicationExtensionRecipientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Type { get; set; }
    public bool IsActive { get; set; }
    public bool IsAssociated { get; set; }
    public bool HasRealAttempt { get; set; }
    public bool HasRealSuccess { get; set; }
    public string? LastStatus { get; set; }
    public bool LastAttemptWasTest { get; set; }
}

public class CommunicationExtensionPreviewDto
{
    public int CommunicationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string Mode { get; set; } = CommunicationExtensionModes.NeverAttempted;
    public int TotalInDirectory { get; set; }
    public int EligibleWithEmail { get; set; }
    public int AlreadySuccessfulCount { get; set; }
    public int NeverAttemptedCount { get; set; }
    public int PreviouslyAttemptedCount { get; set; }
    public int FailedOrPendingCount { get; set; }
    public int AlreadyAssociatedCount { get; set; }
    public int NewToCommunicationCount { get; set; }
    public int SelectedForSendCount { get; set; }
    public int InactiveOrWithoutEmailCount { get; set; }
    public bool IsTestCommunication { get; set; }
    public bool CandidateListTruncated { get; set; }
    public List<CommunicationExtensionRecipientDto> Recipients { get; set; } = [];
}

public class CommunicationExtensionResultDto : CommunicationExtensionPreviewDto
{
    public bool Queued { get; set; }
    public int AddedAssociationCount { get; set; }
    public CommunicationDto? Communication { get; set; }
}

public class CommunicationExtensionSourceData
{
    public int CommunicationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool SendToAllEmails { get; set; }
    public bool IsAccountStatement { get; set; }
    public bool HasWhatsAppChannel { get; set; }
    public int ClientRecipientCount { get; set; }
    public int? EmailChannelContentId { get; set; }
    public List<CommunicationExtensionRecipientDto> Recipients { get; set; } = [];
}
