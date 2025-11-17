using System.Text.Json.Serialization;

namespace GuardeSoftwareAPI.Dtos.Communication
{
    // --- NUEVA CLASE ---
    /** Representa un archivo adjunto que ya existe en el servidor */
    public class AttachmentDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty; // URL pública en tu VPS
    }

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
        
        // --- PROPIEDAD AÑADIDA ---
        // El backend debe llenar esto con los archivos guardados en el VPS
        public List<AttachmentDto> Attachments { get; set; } = new();
    }
    
    public class UpsertCommunicationRequest
    {
        public int? Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? SendDate { get; set; }
        public string? SendTime { get; set; }
        public List<string> Channels { get; set; } = []; // ["Email", "WhatsApp"]
        public List<string> Recipients { get; set; } = []; // ["All Clients", "John Doe"]
        public string Type { get; set; } = string.Empty; // "schedule" or "draft"

        // --- PROPIEDAD AÑADIDA ---
        // (Opcional, pero recomendado para manejar la eliminación de adjuntos existentes)
        public List<string>? AttachmentsToRemove { get; set; }
    }

    



    
    // --- NUEVA CLASE ---
    // DTO para el endpoint de "Reintentar"
    public class RetryRequestDto
    {
        [JsonPropertyName("mailServerId")]
        public string MailServerId { get; set; }
    }
}