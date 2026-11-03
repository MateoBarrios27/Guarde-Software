namespace GuardeSoftwareAPI.Services.chat
{
    /// <summary>
    /// Qué visitantes tienen la página abierta ahora. Decide si una respuesta de la
    /// administración se avisa por email: si el visitante está conectado la ve en el chat.
    /// </summary>
    public interface IChatPresence
    {
        /// <returns>true si con esta conexión el visitante pasó de desconectado a conectado.</returns>
        bool VisitorConnected(Guid publicId, string connectionId);

        /// <returns>La conversación afectada y si quedó sin conexiones; null si la conexión no se conocía.</returns>
        (Guid PublicId, bool NowOffline)? VisitorDisconnected(string connectionId);

        bool IsVisitorOnline(Guid publicId);
    }

    /// <summary>Registro en memoria: la presencia se reconstruye sola cuando los widgets se reconectan.</summary>
    public sealed class ChatPresenceRegistry : IChatPresence
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, Guid> _connections = new();
        private readonly Dictionary<Guid, int> _counts = new();

        public bool VisitorConnected(Guid publicId, string connectionId)
        {
            lock (_gate)
            {
                if (_connections.ContainsKey(connectionId))
                    return false;

                _connections[connectionId] = publicId;
                _counts.TryGetValue(publicId, out int current);
                _counts[publicId] = current + 1;
                return current == 0;
            }
        }

        public (Guid PublicId, bool NowOffline)? VisitorDisconnected(string connectionId)
        {
            lock (_gate)
            {
                if (!_connections.Remove(connectionId, out Guid publicId))
                    return null;

                int remaining = _counts.TryGetValue(publicId, out int current) ? current - 1 : 0;
                if (remaining <= 0)
                {
                    _counts.Remove(publicId);
                    return (publicId, true);
                }

                _counts[publicId] = remaining;
                return (publicId, false);
            }
        }

        public bool IsVisitorOnline(Guid publicId)
        {
            lock (_gate)
            {
                return _counts.ContainsKey(publicId);
            }
        }
    }
}
