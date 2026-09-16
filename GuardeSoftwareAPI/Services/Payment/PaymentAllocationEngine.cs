using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.clientMonthBalance;

namespace GuardeSoftwareAPI.Services.payment;

/// <summary>
/// Replays credits by date: prior principal, accrued interests, current rent,
/// then future periods. The reference period is the credit's month, never today's
/// date, so adding another month does not change what an earlier payment covered.
/// </summary>
public static class PaymentAllocationEngine
{
    public static AllocationResult Allocate(IEnumerable<AccountMovement> ledger)
    {
        var movements = ledger.ToList();
        var debits = movements
            .Where(m => m.MovementType.Equals("DEBITO", StringComparison.OrdinalIgnoreCase) && m.Amount > 0)
            .Select(m => new Debit(m, ClientMonthBalanceService.ResolveMonthStart(m),
                ClientMonthBalanceService.IsInterestConcept(m.Concept)))
            .ToList();
        var rows = debits.GroupBy(d => d.Month).ToDictionary(g => g.Key, g => new ClientMonthBalance
        {
            RentalId = g.First().Movement.RentalId,
            MonthYear = g.Key.ToString("MM/yyyy"),
            Interests = g.Where(d => d.IsInterest).Sum(d => d.Remaining),
            MonthlyDebits = g.Where(d => !d.IsInterest).Sum(d => d.Remaining),
            AllocatedInterests = 0m,
            AllocatedRent = 0m
        });
        var result = new AllocationResult();
        foreach (var credit in movements
            .Where(m => m.MovementType.Equals("CREDITO", StringComparison.OrdinalIgnoreCase) && m.Amount > 0)
            .OrderBy(m => m.MovementDate).ThenBy(m => m.Id))
        {
            var creditMonth = new DateTime(credit.MovementDate.Year, credit.MovementDate.Month, 1);
            decimal remaining = credit.Amount;
            foreach (var debit in debits.Where(d => d.Remaining > 0)
                .OrderBy(d => Priority(d.Month, d.IsInterest, creditMonth))
                .ThenBy(d => d.Month).ThenBy(d => d.IsInterest ? 0 : 1)
                .ThenBy(d => d.Movement.MovementDate).ThenBy(d => d.Movement.Id))
            {
                if (remaining <= 0) break;
                decimal applied = Math.Min(remaining, debit.Remaining);
                result.Allocations.Add(new CreditAllocation(credit.Id, debit.Movement.Id,
                    debit.Movement.Concept ?? "Débito sin concepto", debit.Month,
                    applied, applied < debit.Remaining));
                debit.Remaining -= applied;
                remaining -= applied;
                var row = rows[debit.Month];
                if (debit.IsInterest) row.AllocatedInterests += applied;
                else row.AllocatedRent += applied;
                if (debit.Month > creditMonth) row.AdvancedPayment += applied;
                else row.Paid += applied;
            }
            result.UnallocatedCredits[credit.Id] = remaining;
            if (remaining > 0)
            {
                // Preserve actual credit balances even when no future rent is generated.
                var lastMonth = rows.Count == 0 ? creditMonth : rows.Keys.Max();
                if (!rows.TryGetValue(lastMonth, out var last))
                    rows[lastMonth] = last = new ClientMonthBalance
                    {
                        RentalId = credit.RentalId, MonthYear = lastMonth.ToString("MM/yyyy"),
                        AllocatedInterests = 0m, AllocatedRent = 0m
                    };
                if (lastMonth > creditMonth) last.AdvancedPayment += remaining;
                else last.Paid += remaining;
            }
        }
        decimal carry = 0;
        foreach (var row in rows.OrderBy(p => p.Key).Select(p => p.Value))
        {
            row.PreviousBalance = carry;
            row.Balance = carry + row.Interests + row.MonthlyDebits;
            carry = row.Balance - row.Paid - row.AdvancedPayment;
            result.Rows.Add(row);
        }
        return result;
    }

    internal static int Priority(DateTime debitMonth, bool isInterest, DateTime creditMonth) =>
        !isInterest && debitMonth < creditMonth ? 0 :
        isInterest && debitMonth <= creditMonth ? 1 :
        debitMonth == creditMonth ? 2 : 3;

    private sealed class Debit(AccountMovement movement, DateTime month, bool isInterest)
    {
        public AccountMovement Movement { get; } = movement;
        public DateTime Month { get; } = month;
        public bool IsInterest { get; } = isInterest;
        public decimal Remaining { get; set; } = movement.Amount;
    }

    public sealed class AllocationResult
    {
        public List<ClientMonthBalance> Rows { get; } = [];
        public List<CreditAllocation> Allocations { get; } = [];
        public Dictionary<int, decimal> UnallocatedCredits { get; } = [];
    }

    public sealed record CreditAllocation(int CreditId, int DebitId, string Concept,
        DateTime Month, decimal Amount, bool IsPartial);
}
