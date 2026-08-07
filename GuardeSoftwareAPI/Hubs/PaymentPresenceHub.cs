using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Services.payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GuardeSoftwareAPI.Hubs
{
    [Authorize]
    public sealed class PaymentPresenceHub : Hub
    {
        private readonly PaymentPresenceRegistry _registry;
        private readonly IPaymentStateService _paymentStateService;
        private readonly DaoUser _daoUser;
        private readonly ILogger<PaymentPresenceHub> _logger;

        public PaymentPresenceHub(
            PaymentPresenceRegistry registry,
            IPaymentStateService paymentStateService,
            DaoUser daoUser,
            ILogger<PaymentPresenceHub> logger)
        {
            _registry = registry;
            _paymentStateService = paymentStateService;
            _daoUser = daoUser;
            _logger = logger;
        }

        public static string ClientGroupName(int clientId) => $"payment-client-{clientId}";

        public async Task<PaymentPresenceJoinResult> JoinClientRoom(int clientId)
        {
            if (clientId <= 0)
            {
                throw new HubException("El cliente seleccionado no es valido.");
            }

            var viewer = await ResolveViewerAsync();
            await Groups.AddToGroupAsync(Context.ConnectionId, ClientGroupName(clientId));
            _registry.Join(clientId, Context.ConnectionId, viewer);

            var state = await _paymentStateService.GetSnapshotAsync(clientId);
            var viewers = _registry.GetViewers(clientId);
            await BroadcastPresenceChangedAsync(clientId);

            _logger.LogInformation("{UserName} opened payment presence for client {ClientId}", viewer.UserName, clientId);

            return new PaymentPresenceJoinResult
            {
                ClientId = clientId,
                StateToken = state.Token,
                Viewers = viewers
            };
        }

        public async Task LeaveClientRoom(int clientId)
        {
            if (clientId <= 0)
            {
                return;
            }

            _registry.Leave(clientId, Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ClientGroupName(clientId));
            await BroadcastPresenceChangedAsync(clientId);
        }

        public async Task<PaymentPresenceJoinResult> RefreshClientState(int clientId)
        {
            if (clientId <= 0)
            {
                throw new HubException("El cliente seleccionado no es valido.");
            }

            var state = await _paymentStateService.GetSnapshotAsync(clientId);
            return new PaymentPresenceJoinResult
            {
                ClientId = clientId,
                StateToken = state.Token,
                Viewers = _registry.GetViewers(clientId)
            };
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var clientIds = _registry.RemoveConnection(Context.ConnectionId);
            foreach (var clientId in clientIds)
            {
                await BroadcastPresenceChangedAsync(clientId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private Task BroadcastPresenceChangedAsync(int clientId)
        {
            return Clients.Group(ClientGroupName(clientId)).SendAsync(
                "PresenceChanged",
                new PaymentPresenceChanged
                {
                    ClientId = clientId,
                    Viewers = _registry.GetViewers(clientId)
                });
        }

        private async Task<PaymentPresenceUser> ResolveViewerAsync()
        {
            var identityUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? Context.UserIdentifier
                ?? Context.ConnectionId;
            var userName = Context.User?.FindFirst("username")?.Value
                ?? Context.User?.Identity?.Name
                ?? "usuario";
            var displayName = userName;

            DataTable userTable = await _daoUser.GetUserByIdentityUserId(identityUserId);
            if (userTable.Rows.Count > 0)
            {
                var row = userTable.Rows[0];
                userName = row["username"]?.ToString() ?? userName;
                displayName = row["first_name"]?.ToString() ?? userName;
            }

            return new PaymentPresenceUser
            {
                UserId = identityUserId,
                UserName = userName,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName,
                JoinedAtUtc = DateTime.UtcNow
            };
        }
    }
}
