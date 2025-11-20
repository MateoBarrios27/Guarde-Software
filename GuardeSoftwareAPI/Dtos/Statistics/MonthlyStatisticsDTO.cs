namespace GuardeSoftwareAPI.Dtos.Statistics
{
    public class MonthlyStatisticsDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalCobrado { get; set; }
        public decimal TotalSaldo { get; set; }
        public decimal TotalInteres { get; set; }
        public decimal TotalSaldoAnterior { get; set; }
        public decimal TotalAbono { get; set; }
    }
}
