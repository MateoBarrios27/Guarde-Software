using GuardeSoftwareAPI.Dtos.Chat;
using GuardeSoftwareAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GuardeSoftwareAPI.Services.chat
{
    public sealed class SignalRChatRealtime : IChatRealtime
    {
        private readonly IHubContext<ChatStaffHub> _staff;
        private readonly IHubContext<ChatVisitorHub> _visitor;

        public SignalRChatRealtime(IHubContext<ChatStaffHub> staff, IHubContext<ChatVisitorHub> visitor)
        {
            _staff = staff;
            _visitor = visitor;
        }

        public Task MessageAddedAsync(
            ChatConversationSummaryDto conversation,
            ChatMessageDto staffView,
            ChatVisitorMessageDto visitorView)
        {
            return Task.WhenAll(
                _staff.Clients.Group(ChatStaffHub.Group)
                    .SendAsync("ChatMessage", new { conversation, message = staffView }),
                _visitor.Clients.Group(ChatVisitorHub.GroupName(conversation.PublicId))
                    .SendAsync("ChatMessage", visitorView));
        }

        public Task ConversationChangedAsync(ChatConversationSummaryDto conversation)
            => _staff.Clients.Group(ChatStaffHub.Group).SendAsync("ChatConversation", conversation);

        public Task StatusChangedAsync(ChatConversationSummaryDto conversation)
        {
            return Task.WhenAll(
                _staff.Clients.Group(ChatStaffHub.Group).SendAsync("ChatConversation", conversation),
                _visitor.Clients.Group(ChatVisitorHub.GroupName(conversation.PublicId))
                    .SendAsync("ChatStatus", new { status = conversation.Status }));
        }

        public Task PresenceChangedAsync(Guid publicId, bool online)
            => _staff.Clients.Group(ChatStaffHub.Group).SendAsync("ChatPresence", new { publicId, online });
    }
}
