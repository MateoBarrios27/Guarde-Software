namespace GuardeSoftwareAPI.Dtos.Statistics
{
    public class ClientStatisticsDto
    {
        public int Total { get; set; }
        // Pagaron el mes: activos sin nivel de mora, con próximo pago posterior al mes actual.
        public int AlDia { get; set; }
        // Activos en nivel 1, 2 o 3 (tres o más meses), sin duplicarlos en los otros grupos.
        public int Morosos { get; set; }
        // No pagaron el mes: activos sin nivel de mora que todavía deben contemplar el mes actual.
        public int Pendientes { get; set; }
        public int DadosBaja { get; set; }
    }
}
