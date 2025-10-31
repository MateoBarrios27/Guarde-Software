using System;

namespace GuardeSoftwareAPI.Dtos.Communication
{
    /// <summary>
    /// DTO que representa un historial de comunicación simple
    /// para el modal de detalle de cliente.
    /// </summary>
    public class ClientCommunicationDto
    {
        public int Id { get; set; } // Usará dispatch_id
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty; // "email", "whatsapp", etc.
        public string Subject { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
    }
}
