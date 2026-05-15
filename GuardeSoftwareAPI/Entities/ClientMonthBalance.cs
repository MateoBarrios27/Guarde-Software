namespace GuardeSoftwareAPI.Entities
{
    public class ClientMonthBalance
    {
        public int Id { get; set; }
        public int RentalId { get; set; }
        public string MonthYear { get; set; } = string.Empty;
        public decimal PreviousBalance { get; set; }
        public decimal Interests { get; set; }
        public decimal MonthlyDebits { get; set; }
        public decimal Balance { get; set; }
        public decimal Paid { get; set; }
        public decimal AdvancedPayment { get; set; }
    }
}