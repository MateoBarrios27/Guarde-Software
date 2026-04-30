
namespace GuardeSoftwareAPI.Dtos.PaymentMethod
{
    public class UpdatePaymentMethodDto{
        public string Name { get; set; } = string.Empty;
        public decimal? Commission { get; set; }
    }
}