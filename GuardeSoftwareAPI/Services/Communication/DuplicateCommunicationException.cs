namespace GuardeSoftwareAPI.Services.communication
{
    /// <summary>
    /// Un pedido repetido de crear o enviar un comunicado que ya existe o ya está en cola (doble clic, dos pestañas,
    /// reintento de red). El controlador responde 409 con el mensaje: no se crea ni se encola nada nuevo.
    /// </summary>
    public sealed class DuplicateCommunicationException : Exception
    {
        public int? ExistingCommunicationId { get; }

        public DuplicateCommunicationException(string message, int? existingCommunicationId = null) : base(message)
        {
            ExistingCommunicationId = existingCommunicationId;
        }
    }
}
