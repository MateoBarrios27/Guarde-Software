namespace GuardeSoftwareAPI.Dtos.Communication
{
    public class RecipientForSendingDto
    {
        public int ClientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; } // Assumes a 'phone' or 'whatsapp' column in clients
    }
}