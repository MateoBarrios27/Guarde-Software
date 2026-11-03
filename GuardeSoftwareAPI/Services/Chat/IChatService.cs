using GuardeSoftwareAPI.Dtos.Chat;

namespace GuardeSoftwareAPI.Services.chat
{
    public interface IChatService
    {
        // ---- Visitante (anónimo, autenticado por el token de su conversación)

        Task<ChatStatusDto> GetStatusAsync();
        Task<ChatVisitorConversationDto> StartConversationAsync(StartChatRequest request);
        Task<ChatVisitorConversationDto> GetVisitorConversationAsync(Guid publicId, string? token, long afterMessageId);
        Task<ChatVisitorMessageDto> SendVisitorMessageAsync(Guid publicId, string? token, string? body);
        Task MarkVisitorReadAsync(Guid publicId, string? token, long? upToMessageId);

        /// <summary>Valida el token para abrir la conexión en tiempo real del visitante.</summary>
        Task AuthenticateVisitorAsync(Guid publicId, string? token);

        // ---- Administración (usuarios autenticados de Guarde-Software)

        Task<List<ChatConversationSummaryDto>> GetConversationsAsync(string? status, string? search);
        Task<ChatConversationDetailDto> GetConversationAsync(long conversationId);
        Task<ChatInboxSummaryDto> GetInboxSummaryAsync();
        Task MarkReadAsync(long conversationId, long? upToMessageId);
        Task<ChatStaffReplyResultDto> ReplyAsync(long conversationId, int userId, string? body, CancellationToken cancellationToken = default);
        Task<ChatConversationSummaryDto> CloseAsync(long conversationId, int userId);
        Task<ChatConversationSummaryDto> ReopenAsync(long conversationId);
        Task<ChatBusinessHoursDto> GetBusinessHoursAsync();
        Task<ChatBusinessHoursDto> UpdateBusinessHoursAsync(UpdateChatBusinessHoursRequest request, int userId);
    }
}
