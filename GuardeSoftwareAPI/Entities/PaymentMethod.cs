namespace GuardeSoftwareAPI.Entities
{
    public class PaymentMethod
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g., Cash, Bank Transfer, etc.
        public decimal? Commission { get; set; } // e.g., 24.00 for 24%
    }
}