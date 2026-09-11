using Microsoft.AspNetCore.Http;

namespace GuardeSoftwareAPI.Dtos.Communication
{
    public class ReceiptDeliveryRequest
    {
        public string ClientName { get; set; } = string.Empty;
        public string ReceiptPeriod { get; set; } = string.Empty;
        public List<string> Emails { get; set; } = [];
        public List<string> WhatsAppPhones { get; set; } = [];
        public IFormFile? Receipt { get; set; }
    }

    public class ReceiptDeliveryResult
    {
        public int SuccessfulCount { get; set; }
        public int FailedCount { get; set; }
        public string FileName { get; set; } = string.Empty;
        public List<ReceiptDeliveryAttempt> Attempts { get; set; } = [];
    }

    public class ReceiptDeliveryAttempt
    {
        public string Channel { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
