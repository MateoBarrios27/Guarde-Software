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
        public decimal? AllocatedInterests { get; set; }
        public decimal? AllocatedRent { get; set; }
        public decimal UnpaidInterests => Math.Max(0m, Interests - (AllocatedInterests
            ?? Math.Max(0m, Paid + AdvancedPayment - PreviousBalance)));
        public decimal UnpaidRent => Math.Max(0m, MonthlyDebits - (AllocatedRent
            ?? Math.Max(0m, Paid + AdvancedPayment - PreviousBalance - Interests)));
    }
}
