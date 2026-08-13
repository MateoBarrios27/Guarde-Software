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
        public decimal TotalAdvancePayments { get; set; }
        public decimal TotalIvaFacturaA { get; set; }
        public decimal TotalIvaFacturaB { get; set; }
        public decimal TotalObligacionDelPeriodo { get; set; }
        public decimal TotalAplicadoAlPeriodo { get; set; }
        public decimal TotalPendienteDelPeriodo { get; set; }
        public decimal TotalCobradoParaMesesFuturos { get; set; }
        public decimal TotalCubiertoAntesDelPeriodo { get; set; }
        public int TotalEspacios { get; set; }
        public decimal PorcentajeOcupacion { get; set; }
        public decimal AbonoPromedioPorEspacio { get; set; }
        public List<WarehouseRevenueDto>? WarehouseRevenues { get; set; }
    }
}
