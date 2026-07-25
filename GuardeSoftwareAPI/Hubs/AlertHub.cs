using Microsoft.AspNetCore.SignalR;

namespace GuardeSoftwareAPI.Hubs
{
    /// <summary>
    /// Hub de SignalR para la difusión de alertas del sistema en tiempo real.
    /// También puede usarse en el futuro para notificaciones de webhooks,
    /// actualizaciones de estado de comunicados, pagos, etc.
    /// </summary>
    public class AlertHub : Hub
    {
        private readonly ILogger<AlertHub> _logger;

        public AlertHub(ILogger<AlertHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Cliente SignalR conectado: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Cliente SignalR desconectado: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
