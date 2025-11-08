namespace GuardeSoftwareAPI.Entities
{
    public class Client
    {
        public int? Id { get; set; }
        public decimal? PaymentIdentifier { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; }
        public string? Notes { get; set; } = string.Empty;
        public string? Dni { get; set; } = string.Empty;
        public string? Cuit { get; set; } = string.Empty;
        public int? PreferredPaymentMethodId { get; set; }
        public string? IvaCondition { get; set; } = string.Empty;
        public int? BillingTypeId { get; set; }
        public bool? Active { get; set; }
    }
}
