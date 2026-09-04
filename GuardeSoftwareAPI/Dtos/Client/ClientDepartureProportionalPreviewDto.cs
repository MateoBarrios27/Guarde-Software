namespace GuardeSoftwareAPI.Dtos.Client
{
    public sealed class ClientDepartureProportionalPreviewDto
    {
        public decimal BaseRent { get; set; }
        public decimal ProportionalAmount { get; set; }
        public int DaysToCharge { get; set; }
        public int DaysInMonth { get; set; }
    }
}
