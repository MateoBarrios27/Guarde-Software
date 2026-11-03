namespace GuardeSoftwareAPI.Services.chat
{
    /// <summary>
    /// Avisa por email al visitante que ya se fue de la web. Sólo Email: el canal de
    /// WhatsApp sigue deshabilitado según la política vigente de Comunicaciones.
    /// </summary>
    public interface IChatEmailNotifier
    {
        /// <returns>true si algún servidor SMTP aceptó el mensaje.</returns>
        Task<bool> SendReplyAsync(
            string toEmail,
            string visitorName,
            string staffMessage,
            CancellationToken cancellationToken = default);
    }
}
