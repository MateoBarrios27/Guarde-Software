namespace GuardeSoftwareAPI.Entities
{
    public class Payment
    {
        public int Id { get; set; }
        public int PaymentMethodId { get; set; }
        public int ClientId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal? PaymentIdentifier { get; set; } //using for dashboard
        public string? ClientName {get; set;}

    }
}