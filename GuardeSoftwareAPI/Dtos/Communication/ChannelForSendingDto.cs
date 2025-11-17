namespace GuardeSoftwareAPI.Dtos.Communication
{
        public class ChannelForSendingDto
    {
        public int CommChannelContentId { get; set; }
        public string ChannelName { get; set; } = string.Empty; // "Email" or "WhatsApp"
        public string? Subject { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        
        // --- PROPIEDAD AÑADIDA ---
        // El JSON de los adjuntos que están en el VPS
        public string? AttachmentsJson { get; set; }
    }
}