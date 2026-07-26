using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Hubs
{
    public class CommunicationHub : Hub
    {
        private readonly ILogger<CommunicationHub> _logger;

        public CommunicationHub(ILogger<CommunicationHub> logger)
        {
            _logger = logger;
        }

        public override Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected to CommunicationHub: {Context.ConnectionId}");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client disconnected from CommunicationHub: {Context.ConnectionId}");
            return base.OnDisconnectedAsync(exception);
        }
    }
}
