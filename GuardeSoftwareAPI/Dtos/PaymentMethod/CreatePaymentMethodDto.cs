namespace GuardeSoftwareAPI.Dtos.PaymentMethod
{
    public class CreatePaymentMethodDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal? Commission { get; set; } // e.g., 24.00 for 24%
    }
}