using GuardeSoftwareAPI.Dtos.Chat;

namespace GuardeSoftwareAPI.Services.chat
{
    /// <summary>
    /// Difusión en tiempo real del chat. El servicio solo conoce esta interfaz;
    /// la implementación real usa los hubs de SignalR.
    /// </summary>
    public interface IChatRealtime
    {
        /// <summary>Hay un mensaje nuevo (del visitante o de la administración).</summary>
        Task MessageAddedAsync(
            ChatConversationSummaryDto conversation,
            ChatMessageDto staffView,
            ChatVisitorMessageDto visitorView);

        /// <summary>Cambió la lectura, la asignación o el estado de la conversación.</summary>
        Task ConversationChangedAsync(ChatConversationSummaryDto conversation);

        /// <summary>Se cerró o reabrió: también se avisa al visitante.</summary>
        Task StatusChangedAsync(ChatConversationSummaryDto conversation);

        Task PresenceChangedAsync(Guid publicId, bool online);
    }
}
