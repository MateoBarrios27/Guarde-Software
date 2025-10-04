namespace GuardeSoftwareAPI.Dtos.Client
{
    public class GetTableClientsDto
    {
        public int Id { get; set; }
        public decimal? PaymentIdentifier { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; } = string.Empty;
        public string? Phone { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public int? PreferredPaymentMethodId { get; set; }
        public string? Document { get; set; } = string.Empty;
        public List<string>? Lockers { get; set; } = null;
        public bool Active { get; set; }

    }
}