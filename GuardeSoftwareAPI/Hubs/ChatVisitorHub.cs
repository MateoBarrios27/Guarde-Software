using GuardeSoftwareAPI.Services.chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GuardeSoftwareAPI.Hubs
{
    /// <summary>
    /// Canal del visitante de la web. Es anónimo: una conexión no sirve de nada hasta
    /// que llama a <see cref="Join"/> con el token de su conversación. El token viaja
    /// en el mensaje y no en la URL para que no quede en los logs del proxy.
    /// </summary>
    [AllowAnonymous]
    public sealed class ChatVisitorHub : Hub
    {
        private readonly IChatService _chat;
        private readonly IChatPresence _presence;
        private readonly IChatRealtime _realtime;

        public ChatVisitorHub(IChatService chat, IChatPresence presence, IChatRealtime realtime)
        {
            _chat = chat;
            _presence = presence;
            _realtime = realtime;
        }

        public static string GroupName(Guid publicId) => $"chat-visitor-{publicId:N}";

        public async Task Join(Guid conversationId, string? token)
        {
            try
            {
                await _chat.AuthenticateVisitorAsync(conversationId, token);
            }
            catch (ChatNotFoundException)
            {
                throw new HubException("La conversación no existe.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
            if (_presence.VisitorConnected(conversationId, Context.ConnectionId))
                await _realtime.PresenceChangedAsync(conversationId, true);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            (Guid PublicId, bool NowOffline)? left = _presence.VisitorDisconnected(Context.ConnectionId);
            if (left is { NowOffline: true } result)
                await _realtime.PresenceChangedAsync(result.PublicId, false);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
