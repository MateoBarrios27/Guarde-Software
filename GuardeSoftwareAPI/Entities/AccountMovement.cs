namespace GuardeSoftwareAPI.Entities
{
    public class AccountMovement
    {
        public int Id { get; set; }
        public int RentalId { get; set; }
        public DateTime MovementDate { get; set; }
        public String MovementType { get; set; } = string.Empty;
        public String Concept { get; set; } = string.Empty;
        public Decimal Amount { get; set; }
        public int? PaymentId { get; set; }
    }
}