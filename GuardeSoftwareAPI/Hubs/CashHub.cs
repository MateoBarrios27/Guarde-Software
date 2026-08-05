using Microsoft.AspNetCore.SignalR;

namespace GuardeSoftwareAPI.Hubs
{
    /// <summary>
    /// Hub de SignalR para la sincronización en tiempo real de la Caja (Cash).
    /// </summary>
    public class CashHub : Hub
    {
        private readonly ILogger<CashHub> _logger;

        public CashHub(ILogger<CashHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Cliente de Caja conectado: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Cliente de Caja desconectado: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
