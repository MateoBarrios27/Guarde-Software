using System.Globalization;
using GuardeSoftwareAPI.Dtos.AccountMovement;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.payment;

namespace GuardeSoftwareAPI.Services.accountMovement;

/// <summary>Read-only reconstruction from the current ledger, not an immutable audit snapshot.</summary>
public static class MovementStatisticsCalculator
{
    public static MovementStatisticsDto Calculate(IEnumerable<AccountMovement> ledger,
        AccountMovement movement, decimal? currentRent)
    {
        var prefix = ledger.Where(m => m.RentalId == movement.RentalId &&
            (m.MovementDate < movement.MovementDate ||
             (m.MovementDate == movement.MovementDate && m.Id <= movement.Id))).ToList();
        var allocation = PaymentAllocationEngine.Allocate(prefix);
        var month = new DateTime(movement.MovementDate.Year, movement.MovementDate.Month, 1);
        var priorPrincipal = allocation.Rows
            .Where(r => DateTime.ParseExact(r.MonthYear, "MM/yyyy", CultureInfo.InvariantCulture) < month)
            .Sum(r => r.UnpaidRent);
        var surplus = allocation.UnallocatedCredits.Values.Sum();
        return new MovementStatisticsDto
        {
            MovementId = movement.Id, RentalId = movement.RentalId, MovementDate = movement.MovementDate,
            PreviousBalance = surplus > 0 ? surplus : -priorPrincipal,
            InterestAmount = allocation.Rows.Sum(r => r.UnpaidInterests),
            CurrentRent = currentRent,
            Balance = prefix.Sum(m => m.MovementType.Equals("CREDITO", StringComparison.OrdinalIgnoreCase)
                ? m.Amount : -m.Amount)
        };
    }
}
