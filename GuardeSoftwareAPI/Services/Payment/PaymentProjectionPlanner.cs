using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.clientMonthBalance;

namespace GuardeSoftwareAPI.Services.payment;

public sealed record ProjectedRentDebit(DateTime Month, decimal Amount);
public sealed record PaymentDecisionOption(string Action, string Title, string Description,
    List<ProjectedRentDebit> Debits, decimal ResultingBalance, decimal PreviousBalance, DateTime NextPaymentDate);
public sealed record PaymentDecisionDetails(string Code, string Scenario, string Message,
    decimal Surplus, decimal RemainingDebt, string DecisionToken, string ExpectedPaymentStateToken,
    List<PaymentDecisionOption> Options);
public sealed class PaymentDecisionRequiredException(PaymentDecisionDetails details)
    : Exception("El pago necesita una decisión del administrativo. Revisá el pago con conexión antes de registrarlo.")
{
    public PaymentDecisionDetails Details { get; } = details;
}
public sealed record PaymentProjectionPlan(List<ProjectedRentDebit> Debits, PaymentDecisionDetails? Decision, DateTime? CollectionMonth);

/// <summary>Plans rent issuance and the collection month without changing accounting allocations.</summary>
public static class PaymentProjectionPlanner
{
    public static PaymentProjectionPlan Plan(CreatePaymentTransaction dto,
        PaymentAllocationEngine.AllocationResult before, PaymentAllocationEngine.AllocationResult after,
        int creditId, List<RentalAmountHistory> histories, decimal baseRent, bool leaving,
        bool priceLocked, string stateToken, decimal netAfterFees, List<AccountMovement> ledger, DateTime? previousCollectionMonth)
    {
        var paymentMonth = new DateTime(dto.Date.Year, dto.Date.Month, 1);
        static DateTime Month(ClientMonthBalance row) => DateTime.ParseExact(row.MonthYear, "MM/yyyy", CultureInfo.InvariantCulture);
        var lastRent = after.Rows.LastOrDefault(row => row.MonthlyDebits > 0);
        var lastRentMonth = lastRent == null ? paymentMonth.AddMonths(-1) : Month(lastRent);
        decimal surplus = after.UnallocatedCredits.Values.Sum();
        bool reachedLastRent = lastRent != null && after.Allocations.Any(a => a.CreditId == creditId
            && a.Month == lastRentMonth && !ClientMonthBalanceService.IsInterestConcept(a.Concept));
        var partialRent = after.Rows.LastOrDefault(row => row.UnpaidRent > 0
            && (!previousCollectionMonth.HasValue || Month(row) >= previousCollectionMonth.Value)
            && after.Allocations.Any(a => a.CreditId == creditId && a.Month == Month(row)
                && a.Concept.StartsWith("Alquiler ", StringComparison.OrdinalIgnoreCase)));
        bool canProject = !leaving && !dto.SkipFutureProjection;
        var nextMonth = lastRentMonth.AddMonths(1);
        decimal Price(DateTime month)
        {
            int value = month.Year * 100 + month.Month;
            decimal price = histories.Where(h => h.StartDate.Year * 100 + h.StartDate.Month <= value
                && (!h.EndDate.HasValue || h.EndDate.Value.Year * 100 + h.EndDate.Value.Month >= value))
                .OrderByDescending(h => h.StartDate).FirstOrDefault()?.Amount ?? baseRent;
            if (price <= 0m) throw new ArgumentException("El abono debe ser mayor a cero para generar un débito.");
            return price;
        }
        List<ProjectedRentDebit> single = canProject && !priceLocked
            && (surplus > 0m || reachedLastRent || lastRent == null)
            ? [new(nextMonth, Price(nextMonth))] : [];
        var options = new List<PaymentDecisionOption>();
        List<ClientMonthBalance> Replay(List<ProjectedRentDebit> debits) => PaymentAllocationEngine.Allocate(
            ledger.Concat(debits.Select((d, i) => new AccountMovement {
                Id = int.MaxValue - 100 + i, RentalId = ledger.First().RentalId,
                MovementDate = d.Month, MovementType = "DEBITO", Amount = d.Amount,
                Concept = $"Alquiler {new CultureInfo("es-AR").DateTimeFormat.GetMonthName(d.Month.Month)} {d.Month.Year}"
            }))).Rows;
        DateTime LegacyDate(List<ClientMonthBalance> rows) {
            var today = DateTime.UtcNow.AddHours(-3);
            var currentMonth = new DateTime(today.Year, today.Month, 1);
            var touched = rows.LastOrDefault(r => r.MonthlyDebits > 0 && r.UnpaidRent < r.MonthlyDebits);
            var following = touched == null ? currentMonth : Month(touched).AddMonths(1);
            return following > currentMonth ? following : currentMonth;
        }
        PaymentDecisionOption Option(string action, string title, string description, List<ProjectedRentDebit> debits,
            DateTime? collectionMonth = null) {
            var rows = Replay(debits);
            decimal net = rows.LastOrDefault() is { } last ? last.Balance - last.Paid - last.AdvancedPayment : 0m;
            var active = rows.LastOrDefault(r => r.Balance - r.Paid - r.AdvancedPayment > 0);
            decimal previous = active == null ? Math.Max(0m, -net)
                : active.AdvancedPayment > 0 && active.AdvancedPayment < active.MonthlyDebits ? active.AdvancedPayment
                : -rows.Where(r => Month(r) < Month(active)).Sum(r => r.UnpaidRent);
            var heldMonth = previousCollectionMonth.HasValue
                && rows.Any(r => Month(r) == previousCollectionMonth && r.UnpaidRent > 0) ? previousCollectionMonth : null;
            var explicitDue = collectionMonth ?? heldMonth;
            if (explicitDue.HasValue) {
                var due = rows.FirstOrDefault(r => Month(r) == explicitDue);
                previous = net < 0 ? -net
                    : due is { AdvancedPayment: > 0 } && due.AdvancedPayment < due.MonthlyDebits ? due.AdvancedPayment
                    : -rows.Where(r => Month(r) < explicitDue).Sum(r => r.UnpaidRent);
            }
            return new(action, title, description, debits, -net, previous, explicitDue ?? LegacyDate(rows));
        }
        var noProjection = Option("no_projection", "Registrar sin nuevos débitos",
            "El pago se aplica a los movimientos existentes. El remanente queda a favor; no se crea otro alquiler.", []);
        string? scenario = null;
        string message = "";
        var defaults = single;

        if (dto.IsAdvancePayment && canProject && !leaving)
        {
            // Match the chosen coverage window. Existing planned periods are never
            // duplicated and a selected advance never adds an extra trailing rent.
            var previousLast = before.Rows.LastOrDefault(row => row.MonthlyDebits > 0);
            var coverageStart = previousLast != null
                && previousLast.Balance - previousLast.Paid - previousLast.AdvancedPayment > 0m
                ? Month(previousLast) : paymentMonth;
            var coverageEnd = coverageStart.AddMonths(dto.AdvanceMonths!.Value - 1);
            var chosen = new List<ProjectedRentDebit>();
            for (var month = nextMonth; month <= coverageEnd; month = month.AddMonths(1))
                chosen.Add(new(month, Price(month)));
            defaults = chosen;
            decimal difference = netAfterFees + chosen.Sum(d => d.Amount);
            surplus = Math.Max(0m, -difference);
            if (difference != 0m)
            {
                scenario = difference < 0m ? "advance_surplus" : "advance_shortfall";
                message = difference < 0m
                    ? "El importe supera los meses elegidos. Confirmá si querés mantener esos meses y dejar la diferencia a favor."
                    : "El importe no cancela todos los meses elegidos. Podés conservarlos con deuda pendiente o registrar solamente el pago.";
                var selected = Option("selected_months", "Mantener los meses elegidos",
                    "Se generan solamente los alquileres del período seleccionado que todavía no existen. No se agrega un mes extra.", chosen);
                if (difference < 0m) options.Add(selected);
                options.Add(noProjection);
                if (difference > 0m) options.Add(selected);
            }
        }
        else if (lastRent == null && canProject && !priceLocked)
        {
            scenario = "missing_rent";
            message = "La cuenta no tiene un débito de alquiler. Confirmá si corresponde generarlo para el mes del pago.";
            options.Add(noProjection);
            options.Add(Option("single_next", "Generar el alquiler del mes del pago",
                "Se genera un solo débito, con el abono vigente para ese período.", single));
        }
        else if (surplus > 0m)
        {
            scenario = "surplus";
            message = "El mes anterior quedó cancelado. Se generará el próximo débito: elegí si ese mes con saldo a favor sigue pendiente o se considera pagado para la próxima cobranza.";
            if (single.Count > 0) {
                options.Add(Option("credit_next_month", "Dejar el próximo mes pendiente, con saldo a favor",
                    "Se genera el próximo débito y se aplica el excedente. Ese mes queda como próximo pago.", single, nextMonth));
                var two = new List<ProjectedRentDebit>(single) { new(nextMonth.AddMonths(1), Price(nextMonth.AddMonths(1))) };
                options.Add(Option("close_credited_month", "Dar ese mes por pagado y pasar al siguiente",
                    "Se aplica el excedente al próximo débito y se genera el del mes siguiente. Lo que falte pagar se conserva como deuda anterior.", two, nextMonth.AddMonths(1)));
            } else options.Add(noProjection);
            if (leaving) message = "El cliente está marcado como SE VA. El excedente queda a favor, sin alquileres futuros.";
            else if (priceLocked) message = "El cliente tiene un período congelado. El excedente queda a favor sin extenderlo automáticamente.";
            else if (dto.SkipFutureProjection) message = "Se eligió no proyectar nuevos alquileres. Confirmá que el excedente quede a favor.";
        }
        else if (partialRent != null && !leaving)
        {
            scenario = "partial_payment";
            message = "El débito generado quedó parcialmente pagado. Elegí si todavía se debe cobrar ese mes o si corresponde pasar al siguiente conservando la deuda.";
            options.Add(Option("keep_month_pending", "Este mes todavía se tiene que pagar",
                "No se genera otro débito. La fecha de próximo pago sigue siendo el mes parcialmente pagado.", [], Month(partialRent)));
            var followingMonth = Month(partialRent).AddMonths(1);
            bool alreadyExists = after.Rows.Any(r => Month(r) == followingMonth && r.MonthlyDebits > 0);
            var followingDebits = alreadyExists ? new List<ProjectedRentDebit>() : [new ProjectedRentDebit(followingMonth, Price(followingMonth))];
            if (canProject && !priceLocked)
                options.Add(Option("close_partial_month", "Tomar este mes como pagado y pasar al siguiente",
                    alreadyExists
                        ? "El débito del mes siguiente ya existe y se conserva. Lo que faltó pagar queda en Saldo anterior; no se perdona la deuda."
                        : "Se genera el débito del mes siguiente. Lo que faltó pagar queda en Saldo anterior; no se perdona la deuda.", followingDebits, followingMonth));
        }

        DateTime? preservedMonth = previousCollectionMonth.HasValue
            && Replay(scenario == null ? defaults : options.FirstOrDefault(o => o.Action == dto.FutureDebitAction)?.Debits ?? []).Any(r => Month(r) == previousCollectionMonth && r.UnpaidRent > 0)
            ? previousCollectionMonth : null;
        if (scenario == null) return new(defaults, null, preservedMonth);
        // Bind the approval to both the original state and all financially relevant inputs.
        string fingerprint = JsonSerializer.Serialize(new { stateToken, dto.ClientId, dto.PaymentMethodId,
            dto.Amount, dto.Date, dto.IsAdvancePayment, dto.AdvanceMonths, dto.CommissionAmount,
            dto.NewRentAmount, dto.AppliedIncreases, dto.SkipFutureProjection, dto.SurchargeAction,
            dto.SurchargeAmount, dto.SurchargeAmountWasOverridden, scenario, options });
        string token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
        var decision = new PaymentDecisionDetails("PAYMENT_DECISION_REQUIRED", scenario, message,
            surplus, Math.Max(0m, netAfterFees), token, stateToken, options);
        var selectedOption = options.FirstOrDefault(o => o.Action == dto.FutureDebitAction);
        bool explicitMonth = dto.FutureDebitAction is "credit_next_month" or "close_credited_month" or "keep_month_pending" or "close_partial_month";
        return new(selectedOption?.Debits ?? [], decision,
            explicitMonth ? selectedOption?.NextPaymentDate : preservedMonth);
    }
}
