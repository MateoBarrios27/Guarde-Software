using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GuardeSoftwareAPI.Hubs
{
    /// <summary>
    /// Canal de la administración para el chat en vivo. Sólo difunde del servidor al
    /// cliente: las acciones (responder, cerrar, marcar leído) pasan por la API REST,
    /// donde el middleware de Observador ya bloquea cualquier escritura.
    /// </summary>
    [Authorize]
    public sealed class ChatStaffHub : Hub
    {
        public const string Group = "chat-staff";

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Group);
            await base.OnConnectedAsync();
        }
    }
}
