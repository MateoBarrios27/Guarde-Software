namespace GuardeSoftwareAPI.Dtos.Chat
{
    /// <summary>Estado público del chat: lo consulta el widget antes de mostrar la promesa de respuesta.</summary>
    public sealed class ChatStatusDto
    {
        public bool IsOpen { get; set; }
        public int ResponseTimeMinutes { get; set; }
        /// <summary>Ej.: "15:30 hs". Sólo con el chat abierto.</summary>
        public string? ClosesAtLabel { get; set; }
        /// <summary>Ej.: "el lunes a las 9:30 hs". Sólo con el chat cerrado.</summary>
        public string? NextOpenLabel { get; set; }
        /// <summary>Ej.: "Lun a vie 9:30 a 15:30 hs · Sáb 9:30 a 12:30 hs".</summary>
        public string ScheduleLabel { get; set; } = string.Empty;
    }

    /// <summary>Mensaje tal como lo ve la administración.</summary>
    public sealed class ChatMessageDto
    {
        public long Id { get; set; }
        /// <summary>"visitor" o "staff".</summary>
        public string SenderType { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool ReceivedOutOfHours { get; set; }
        /// <summary>sent | failed | no_email | visitor_online | limit. Sólo respuestas de la administración.</summary>
        public string? EmailStatus { get; set; }
    }

    /// <summary>Mensaje tal como lo ve el visitante: nunca expone nombres internos ni estado de email.</summary>
    public sealed class ChatVisitorMessageDto
    {
        public long Id { get; set; }
        public string SenderType { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class ChatConversationSummaryDto
    {
        public long Id { get; set; }
        public Guid PublicId { get; set; }
        public string VisitorName { get; set; } = string.Empty;
        public string? VisitorEmail { get; set; }
        public string? VisitorPhone { get; set; }
        public string? SourceUrl { get; set; }
        /// <summary>"open" o "closed".</summary>
        public string Status { get; set; } = "open";
        public DateTime CreatedAt { get; set; }
        public DateTime LastMessageAt { get; set; }
        public string LastMessagePreview { get; set; } = string.Empty;
        public string? LastMessageSender { get; set; }
        /// <summary>El visitante escribió y la administración todavía no respondió.</summary>
        public bool AwaitingReply { get; set; }
        /// <summary>Instante del primer mensaje sin responder: arranca el reloj de 2 minutos.</summary>
        public DateTime? AwaitingReplySince { get; set; }
        /// <summary>Alguno de los mensajes sin responder llegó con el chat cerrado.</summary>
        public bool AwaitingOutOfHours { get; set; }
        public int UnreadCount { get; set; }
        public bool VisitorOnline { get; set; }
        public string? AssignedUserName { get; set; }
    }

    public sealed class ChatConversationDetailDto
    {
        public ChatConversationSummaryDto Conversation { get; set; } = new();
        public List<ChatMessageDto> Messages { get; set; } = [];
    }

    /// <summary>Resultado de responder: el estado del email importa cuando el visitante ya se fue.</summary>
    public sealed class ChatStaffReplyResultDto
    {
        public ChatMessageDto Message { get; set; } = new();
        public ChatConversationSummaryDto Conversation { get; set; } = new();
    }

    /// <summary>Resumen liviano para el contador del menú.</summary>
    public sealed class ChatInboxSummaryDto
    {
        public int AwaitingReplyCount { get; set; }
        public int UnreadConversationCount { get; set; }
        public DateTime? OldestAwaitingSince { get; set; }
        public bool IsOpenNow { get; set; }
        public int ResponseTimeMinutes { get; set; }
    }

    public sealed class ChatBusinessDayDto
    {
        /// <summary>1 = lunes ... 7 = domingo.</summary>
        public int DayOfWeek { get; set; }
        public bool IsOpen { get; set; }
        /// <summary>HH:mm, hora de Argentina.</summary>
        public string? OpensAt { get; set; }
        public string? ClosesAt { get; set; }
    }

    public sealed class ChatBusinessHoursDto
    {
        public List<ChatBusinessDayDto> Days { get; set; } = [];
        public ChatStatusDto Status { get; set; } = new();
    }

    /// <summary>Conversación vista por el visitante. El token sólo viaja al crearla.</summary>
    public sealed class ChatVisitorConversationDto
    {
        public Guid ConversationId { get; set; }
        public string? VisitorToken { get; set; }
        public string Status { get; set; } = "open";
        public string VisitorName { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
        public DateTime LastActivityAt { get; set; }
        public List<ChatVisitorMessageDto> Messages { get; set; } = [];
        public ChatStatusDto Chat { get; set; } = new();
    }

    public sealed class StartChatRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Message { get; set; }
        public string? SourceUrl { get; set; }
        /// <summary>Señuelo anti-bots: una persona nunca lo completa.</summary>
        public string? Website { get; set; }
    }

    public sealed class SendChatMessageRequest
    {
        public string? Body { get; set; }
    }

    public sealed class MarkChatReadRequest
    {
        /// <summary>Último mensaje que la persona llegó a ver. Si falta se usa el último existente.</summary>
        public long? UpToMessageId { get; set; }
    }

    public sealed class UpdateChatBusinessHoursRequest
    {
        public List<ChatBusinessDayDto> Days { get; set; } = [];
    }
}
