namespace GuardeSoftwareAPI.Entities
{
    public class Address
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty; 
        public string Province { get; set; } = string.Empty;
        
    }
}