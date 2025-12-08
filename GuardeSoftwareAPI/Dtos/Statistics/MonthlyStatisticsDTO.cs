namespace GuardeSoftwareAPI.Dtos.Statistics
{
    public class MonthlyStatisticsDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal TotalAlquileres { get; set; }
        public decimal TotalIntereses { get; set; }
        public decimal DeudaTotalDelMes { get; set; }
        public decimal BalanceGlobalActual { get; set; }
        public int TotalEspaciosOcupados { get; set; }

    }
}
