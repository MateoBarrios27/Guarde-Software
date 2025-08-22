namespace GuardeSoftwareAPI.Entities
{
    public class Email
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // client can have different types of emails (personal, work, etc.)
        
    }
}
