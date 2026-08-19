namespace GuardeSoftwareAPI.Dtos.Communication
{
    public class CommunicationDispatchDto
    {
        public int DispatchId { get; set; }
        public int ClientId { get; set; }
        public int? ExternalRecipientId { get; set; }
        public bool IsExternalRecipient { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string? RecipientPhone { get; set; }
        public bool IsSelected { get; set; } = true;
        public bool HasContent { get; set; }
    }
}
