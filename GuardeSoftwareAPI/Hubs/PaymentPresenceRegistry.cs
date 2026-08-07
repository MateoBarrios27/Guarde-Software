using System.Collections.Concurrent;

namespace GuardeSoftwareAPI.Hubs
{
    public sealed class PaymentPresenceUser
    {
        public string UserId { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public DateTime JoinedAtUtc { get; init; }
    }

    public sealed class PaymentPresenceChanged
    {
        public int ClientId { get; init; }
        public IReadOnlyList<PaymentPresenceUser> Viewers { get; init; } = Array.Empty<PaymentPresenceUser>();
    }

    public sealed class PaymentPresenceJoinResult
    {
        public int ClientId { get; init; }
        public string StateToken { get; init; } = string.Empty;
        public IReadOnlyList<PaymentPresenceUser> Viewers { get; init; } = Array.Empty<PaymentPresenceUser>();
    }

    public sealed class PaymentCompletedNotice
    {
        public int ClientId { get; init; }
        public string PayerName { get; init; } = "Otro usuario";
        public string PayerUserName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public DateTime PaymentDate { get; init; }
        public DateTime RecordedAtUtc { get; init; }
        public string? Concept { get; init; }
    }

    public sealed class PaymentPresenceRegistry
    {
        private static readonly TimeSpan LatestPaymentRetention = TimeSpan.FromMinutes(15);
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, PaymentPresenceUser>> _viewersByClient = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _clientsByConnection = new();
        private readonly ConcurrentDictionary<int, PaymentCompletedNotice> _latestPaymentByClient = new();

        public void Join(int clientId, string connectionId, PaymentPresenceUser viewer)
        {
            var clientViewers = _viewersByClient.GetOrAdd(clientId, _ => new ConcurrentDictionary<string, PaymentPresenceUser>());
            clientViewers[connectionId] = viewer;

            var connectionClients = _clientsByConnection.GetOrAdd(connectionId, _ => new ConcurrentDictionary<int, byte>());
            connectionClients[clientId] = 0;
        }

        public void Leave(int clientId, string connectionId)
        {
            if (_viewersByClient.TryGetValue(clientId, out var clientViewers))
            {
                clientViewers.TryRemove(connectionId, out _);
                if (clientViewers.IsEmpty)
                {
                    _viewersByClient.TryRemove(clientId, out _);
                }
            }

            if (_clientsByConnection.TryGetValue(connectionId, out var connectionClients))
            {
                connectionClients.TryRemove(clientId, out _);
                if (connectionClients.IsEmpty)
                {
                    _clientsByConnection.TryRemove(connectionId, out _);
                }
            }
        }

        public IReadOnlyList<int> GetClientIds(string connectionId)
        {
            return _clientsByConnection.TryGetValue(connectionId, out var clientIds)
                ? clientIds.Keys.ToArray()
                : Array.Empty<int>();
        }

        public IReadOnlyList<int> RemoveConnection(string connectionId)
        {
            var clientIds = GetClientIds(connectionId);
            foreach (var clientId in clientIds)
            {
                Leave(clientId, connectionId);
            }

            return clientIds;
        }

        public IReadOnlyList<PaymentPresenceUser> GetViewers(int clientId)
        {
            if (!_viewersByClient.TryGetValue(clientId, out var clientViewers))
            {
                return Array.Empty<PaymentPresenceUser>();
            }

            return clientViewers.Values
                .OrderBy(viewer => viewer.JoinedAtUtc)
                .ToArray();
        }

        public void RecordPayment(PaymentCompletedNotice notice)
        {
            _latestPaymentByClient[notice.ClientId] = notice;
        }

        public PaymentCompletedNotice? GetLatestPayment(int clientId)
        {
            if (!_latestPaymentByClient.TryGetValue(clientId, out var notice))
            {
                return null;
            }

            if (DateTime.UtcNow - notice.RecordedAtUtc <= LatestPaymentRetention)
            {
                return notice;
            }

            _latestPaymentByClient.TryRemove(clientId, out _);
            return null;
        }
    }
}
