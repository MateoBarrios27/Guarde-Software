namespace GuardeSoftwareAPI.Services.chat
{
    /// <summary>Datos inválidos enviados por el visitante o por la administración.</summary>
    public sealed class ChatValidationException : Exception
    {
        public ChatValidationException(string message) : base(message) { }
    }

    /// <summary>
    /// Conversación inexistente o token incorrecto. Se responde igual en ambos casos
    /// para que nadie pueda averiguar qué identificadores existen.
    /// </summary>
    public sealed class ChatNotFoundException : Exception
    {
        public ChatNotFoundException() : base("La conversación no existe.") { }
    }

    public sealed class ChatRateLimitException : Exception
    {
        public ChatRateLimitException(string message) : base(message) { }
    }

    /// <summary>La acción no corresponde al estado actual (por ejemplo responder una conversación cerrada).</summary>
    public sealed class ChatConflictException : Exception
    {
        public ChatConflictException(string message) : base(message) { }
    }
}
