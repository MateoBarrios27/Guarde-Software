namespace GuardeSoftwareAPI.Entities
{
    public class PaymentMethod
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g., Cash, Bank Transfer, etc.
    }
}