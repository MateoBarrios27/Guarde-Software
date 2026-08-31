namespace GuardeSoftwareAPI.Entities
{
    /// <summary>
    /// Contacto externo de la base de destinatarios para comunicados masivos.
    /// No representa a un cliente operativo de Guarde.
    /// </summary>
    public class MassCommunicationRecipient
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Type { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
