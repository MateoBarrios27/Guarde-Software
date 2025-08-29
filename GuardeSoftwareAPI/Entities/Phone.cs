namespace GuardeSoftwareAPI.Entities
{
    public class Phone
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string? Type { get; set; } = string.Empty; // client can have different types of phones (mobile, home, work, etc.)
        public bool Whatsapp { get; set; }
    }
}
