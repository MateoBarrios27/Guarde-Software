using System.Globalization;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.payment;

public static class LatePaymentSurchargeCalculator
{
    public static LatePaymentSurchargeProjection Project(
        IEnumerable<ClientMonthBalance> balances,
        DateTime paymentDate,
        decimal currentRentFallback)
    {
        var paymentMonth = new DateTime(paymentDate.Year, paymentDate.Month, 1);
        var components = balances
            .Select(balance => new
            {
                Balance = balance,
                Month = DateTime.TryParseExact(
                    balance.MonthYear,
                    "MM/yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedMonth)
                    ? parsedMonth
                    : DateTime.MaxValue
            })
            .Where(item => item.Month <= paymentMonth)
            .OrderBy(item => item.Month)
            .ThenBy(item => item.Balance.Id)
            .Select(item => new LatePaymentComponent
            {
                Month = item.Month,
                UnpaidInterest = item.Balance.UnpaidInterests,
                UnpaidRent = item.Balance.UnpaidRent
            })
            .ToList();

        decimal lateRentBase = components
            .Where(component => component.Month == paymentMonth)
            .Select(component => component.UnpaidRent)
            .LastOrDefault();

        if (!components.Any(component => component.Month == paymentMonth))
        {
            lateRentBase = Math.Max(0m, currentRentFallback);
        }

        // El corte del día 10 congela tanto el alquiler vencido como todos los
        // intereses que estaban impagos. Un crédito posterior puede cancelarlos
        // contablemente, pero no reduce la penalización ya causada por pagarlos tarde.
        decimal unpaidInterestsAtCutoff = components.Sum(component => component.UnpaidInterest);
        decimal taxableBase = lateRentBase + unpaidInterestsAtCutoff;
        return new LatePaymentSurchargeProjection
        {
            LateRentBase = lateRentBase,
            UnpaidInterestsAtCutoff = unpaidInterestsAtCutoff,
            TaxableBase = taxableBase,
            SurchargeAmount = RoundDownToHundred(taxableBase * 0.10m)
        };
    }

    public static decimal Calculate(decimal lateRentBase, decimal unpaidInterestsAtCutoff) =>
        RoundDownToHundred((Math.Max(0m, lateRentBase) + Math.Max(0m, unpaidInterestsAtCutoff)) * 0.10m);

    private static decimal RoundDownToHundred(decimal amount) =>
        amount <= 0m ? 0m : Math.Floor(amount / 100m) * 100m;

    private sealed class LatePaymentComponent
    {
        public DateTime Month { get; init; }
        public decimal UnpaidInterest { get; init; }
        public decimal UnpaidRent { get; init; }
    }
}

public sealed class LatePaymentSurchargeProjection
{
    public decimal LateRentBase { get; init; }
    public decimal UnpaidInterestsAtCutoff { get; init; }
    public decimal TaxableBase { get; init; }
    public decimal SurchargeAmount { get; init; }
}
