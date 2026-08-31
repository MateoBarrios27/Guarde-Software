namespace GuardeSoftwareAPI.Dtos.Client
{
    public class ClientDepartureActionDto
    {
        public string Action { get; set; } = string.Empty;
        public bool ChargeProportional { get; set; }
        public bool RemoveNextMonthDebit { get; set; }
        public DateTime? DepartureDate { get; set; }
        public string? PendingSurchargeAction { get; set; }
    }
}
