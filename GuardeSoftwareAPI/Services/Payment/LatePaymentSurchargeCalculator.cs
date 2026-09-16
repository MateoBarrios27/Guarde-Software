using System.Globalization;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.payment;

public static class LatePaymentSurchargeCalculator
{
    public static LatePaymentSurchargeProjection Project(
        IEnumerable<ClientMonthBalance> balances,
        DateTime paymentDate,
        decimal paymentAvailableForDebt,
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

        decimal remainingPayment = Math.Max(0m, paymentAvailableForDebt);
        var targets = components.SelectMany(component => new[]
        {
            (Component: component, IsInterest: false),
            (Component: component, IsInterest: true)
        }).OrderBy(target => PaymentAllocationEngine.Priority(target.Component.Month, target.IsInterest, paymentMonth))
          .ThenBy(target => target.Component.Month);

        foreach (var target in targets)
        {
            decimal outstanding = target.IsInterest ? target.Component.UnpaidInterest : target.Component.UnpaidRent;
            decimal applied = Math.Min(remainingPayment, outstanding);
            if (target.IsInterest) target.Component.UnpaidInterest -= applied;
            else target.Component.UnpaidRent -= applied;
            remainingPayment -= applied;
        }

        decimal unpaidInterestsAfterPayment = components.Sum(component => component.UnpaidInterest);
        decimal taxableBase = lateRentBase + unpaidInterestsAfterPayment;
        return new LatePaymentSurchargeProjection
        {
            LateRentBase = lateRentBase,
            UnpaidInterestsAfterPayment = unpaidInterestsAfterPayment,
            TaxableBase = taxableBase,
            SurchargeAmount = RoundDownToHundred(taxableBase * 0.10m)
        };
    }

    public static decimal Calculate(decimal lateRentBase, decimal unpaidInterestsAfterPayment) =>
        RoundDownToHundred((Math.Max(0m, lateRentBase) + Math.Max(0m, unpaidInterestsAfterPayment)) * 0.10m);

    private static decimal RoundDownToHundred(decimal amount) =>
        amount <= 0m ? 0m : Math.Floor(amount / 100m) * 100m;

    private sealed class LatePaymentComponent
    {
        public DateTime Month { get; init; }
        public decimal UnpaidInterest { get; set; }
        public decimal UnpaidRent { get; set; }
    }
}

public sealed class LatePaymentSurchargeProjection
{
    public decimal LateRentBase { get; init; }
    public decimal UnpaidInterestsAfterPayment { get; init; }
    public decimal TaxableBase { get; init; }
    public decimal SurchargeAmount { get; init; }
}
