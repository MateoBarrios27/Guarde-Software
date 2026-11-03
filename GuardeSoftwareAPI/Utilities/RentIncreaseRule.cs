namespace GuardeSoftwareAPI.Utils
{
    /// <summary>
    /// Rent increase as the operator confirms it in Finanzas, Dashboard and the client
    /// payment planning (roundRentToPaymentStep in payment-planning.util.ts): the percentage
    /// configured for the month is applied to the current rent and the result is always
    /// rounded UP to the unit of the client's preferred payment method. Cash uses whole
    /// thousands; every other method uses whole hundreds.
    /// </summary>
    public static class RentIncreaseRule
    {
        public static bool IsCashPaymentMethod(string? paymentMethodName)
        {
            return paymentMethodName?.Contains("efectivo", StringComparison.OrdinalIgnoreCase) == true;
        }

        public static decimal PaymentStep(string? paymentMethodName)
        {
            return IsCashPaymentMethod(paymentMethodName) ? 1000m : 100m;
        }

        /// <summary>
        /// Rent after applying <paramref name="percentage"/>. Without a positive percentage
        /// or a base rent the amount is returned untouched, so history is never rounded.
        /// Decimal arithmetic is exact: 100000 + 10% is 110000, not 111000.
        /// </summary>
        public static decimal Apply(decimal baseRent, decimal percentage, string? paymentMethodName)
        {
            if (baseRent <= 0m || percentage <= 0m)
                return baseRent;

            decimal step = PaymentStep(paymentMethodName);
            decimal target = baseRent * (1m + percentage / 100m);
            return Math.Ceiling(target / step) * step;
        }
    }
}
