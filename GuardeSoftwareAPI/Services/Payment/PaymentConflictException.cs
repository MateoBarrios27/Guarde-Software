namespace GuardeSoftwareAPI.Services.payment
{
    public sealed class PaymentConflictDetails
    {
        public string Code { get; init; } = "PAYMENT_STATE_CHANGED";
        public int ClientId { get; init; }
        public string Message { get; init; } = "El estado de cuenta cambio mientras preparabas el pago.";
        public string? RegisteredByName { get; init; }
        public decimal? Amount { get; init; }
        public DateTime? RegisteredAt { get; init; }
        public string? Concept { get; init; }
        public bool SameConcept { get; init; }
    }

    public sealed class PaymentConflictException : Exception
    {
        public PaymentConflictException(PaymentConflictDetails details)
            : base(details.Message)
        {
            Details = details;
        }

        public PaymentConflictDetails Details { get; }
    }
}
