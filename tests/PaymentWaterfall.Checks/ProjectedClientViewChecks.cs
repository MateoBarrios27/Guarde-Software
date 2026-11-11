using System.Globalization;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Services.client;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

// Clients "projected next month" view: every figure must be the one the projected
// account statement (GetClientFinancialData(isNextMonth: true)) would send.
internal static class ProjectedClientViewChecks
{
    public static async Task RunAsync(AccessDB db, ClientMonthBalanceService balances, Action<bool, string> check)
    {
        var now = DateTime.UtcNow.AddHours(-3);
        var month = new DateTime(now.Year, now.Month, 1);
        var nextMonth = month.AddMonths(1);
        var culture = new CultureInfo("es-AR");
        string Concept(DateTime date) => $"Alquiler {culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(date.Month))} {date.Year}";
        var statements = new CommunicationDao(db);
        var service = new ClientService(db, NullLogger<ClientService>.Instance, null!, null!, null!, null!, null!, null!, null!, null!, balances);

        // The harness seeds Ariadna (client 1) for the waterfall suite; keep it out of this one.
        await db.ExecuteCommandAsync("UPDATE clients SET is_deleted = 1 WHERE client_id = 1");

        const int CashMethod = 9301;
        const int BankMethod = 9302;
        await db.ExecuteCommandAsync("INSERT payment_methods VALUES(9301, 'Efectivo', 0, 1), (9302, 'Transferencia', 0, 1);");

        async Task AddClient(int id, decimal identifier, decimal pendingSurcharge = 0m, DateTime? increaseAnchor = null,
            bool clientActive = true, bool rentalActive = true, bool deleted = false, decimal rent = 100m,
            int paymentMethodId = 1, DateTime? priceLockEnd = null, string? departure = null)
        {
            await db.ExecuteCommandAsync("""
                INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount,
                    active, is_deleted, increase_frequency_months, registration_date, receive_communications, departure_status)
                VALUES(@id, 'Vista proyectada prueba', @identifier, @method, @rent, @clientActive, @deleted, 4, @start, 1, @departure);
                SET IDENTITY_INSERT rentals ON;
                INSERT rentals(rental_id, client_id, start_date, active, months_unpaid, pending_surcharge, increase_anchor_date, price_lock_end_date)
                VALUES(@id, @id, @start, @rentalActive, 0, @pending, @anchor, @lock);
                SET IDENTITY_INSERT rentals OFF;
                INSERT rental_amount_history(rental_id, amount, start_date) VALUES(@id, @rent, @start);
                """, [new("@id", id), new("@identifier", identifier), new("@start", month.AddMonths(-2)),
                    new("@clientActive", clientActive), new("@deleted", deleted), new("@rentalActive", rentalActive),
                    new("@pending", pendingSurcharge), new("@rent", rent), new("@anchor", (object?)increaseAnchor ?? DBNull.Value),
                    new("@method", paymentMethodId), new("@lock", (object?)priceLockEnd ?? DBNull.Value),
                    new("@departure", (object?)departure ?? DBNull.Value)]);
        }

        // Rent of a client with a due increase and the month's percentage: debt in the current month only,
        // so the next-month balance does not exist and the projection computes the rent itself.
        async Task AddIncreaseClient(int id, decimal identifier, decimal rent, int paymentMethodId = 1,
            DateTime? anchor = null, DateTime? priceLockEnd = null, string? departure = null)
        {
            await AddClient(id, identifier, increaseAnchor: anchor ?? nextMonth, rent: rent,
                paymentMethodId: paymentMethodId, priceLockEnd: priceLockEnd, departure: departure);
            await AddMovement(id, month, Concept(month), rent);
            await balances.RebuildForRentalAsync(id);
        }

        async Task SetIncreasePercentage(DateTime effectiveMonth, decimal? percentage)
        {
            await db.ExecuteCommandAsync("DELETE FROM monthly_increase_settings WHERE effective_date = @month",
                [new("@month", effectiveMonth)]);
            if (percentage.HasValue)
                await db.ExecuteCommandAsync("INSERT monthly_increase_settings(effective_date, percentage) VALUES(@month, @percentage)",
                    [new("@month", effectiveMonth), new("@percentage", percentage.Value)]);
        }

        async Task AddMovement(int id, DateTime date, string concept, decimal amount, bool credit = false)
        {
            await db.ExecuteCommandAsync("""
                INSERT account_movements(rental_id, movement_date, movement_type, concept, amount)
                VALUES(@id, @date, @type, @concept, @amount);
                """, [new("@id", id), new("@date", date), new("@type", credit ? "CREDITO" : "DEBITO"),
                    new("@concept", concept), new("@amount", amount)]);
        }

        async Task<ClientProjectedNextMonthDto> Projected(int id) =>
            (await service.GetProjectedNextMonthAsync()).Items.Single(item => item.ClientId == id);

        // Every row: table values are the statement values with the table's sign convention.
        async Task AssertMirrorsStatement(int id, decimal identifier, string scenario)
        {
            var statement = await statements.GetClientFinancialData(id, isNextMonth: true);
            var row = await Projected(id);
            check(row.PreviousBalance == (statement.PreviousBalance == 0m ? 0m : -statement.PreviousBalance)
                  && row.InterestAmount == statement.Surcharge
                  && row.StatementTotal == statement.CurrentBalance
                  && row.CurrentRent == statement.Rent
                  && row.IncreasePercentage == statement.RentIncreasePercentage
                  && row.IncreasePercentageMissing == statement.RentIncreasePercentageMissing,
                $"{scenario}: previous balance, interest, rent, increase and total are the projected statement's");
            check(statement.PaymentIdentifier == identifier,
                $"{scenario}: statement exposes the client's payment identifier");
            check(statement.CurrentBalance == (statement.NetDebt > 0m ? statement.NetDebt + identifier : 0m),
                $"{scenario}: statement total is the signed debt plus identifier cents, or zero");
            check(row.Balance == (statement.NetDebt == 0m ? 0m : -statement.NetDebt),
                $"{scenario}: balance is the signed projected debt without identifier cents");
        }

        // 1. Debt this month and no next-month row yet: the current rent becomes previous balance.
        await AddClient(9201, 0.31m);
        await AddMovement(9201, month, Concept(month), 100m);
        await balances.RebuildForRentalAsync(9201);
        var unpaid = await Projected(9201);
        check(unpaid.PreviousBalance == -100m && unpaid.InterestAmount == 0m && unpaid.CurrentRent == 100m && unpaid.Balance == -200m,
            "unpaid month: previous balance -100, rent 100, balance -200");
        check(unpaid.StatementTotal == 200.31m, "unpaid month: statement total adds identifier cents to the debt");
        await AssertMirrorsStatement(9201, 0.31m, "unpaid month");

        // 2. Paid up: only the next rent is projected.
        await AddClient(9202, 0.42m);
        await AddMovement(9202, month, Concept(month), 100m);
        await AddMovement(9202, month, "Pago", 100m, credit: true);
        await balances.RebuildForRentalAsync(9202);
        var settled = await Projected(9202);
        check(settled.PreviousBalance == 0m && settled.CurrentRent == 100m && settled.Balance == -100m,
            "paid month: no previous balance, balance is next rent");
        await AssertMirrorsStatement(9202, 0.42m, "paid month");

        // 3. Credit larger than the next rent: the statement asks for $0 but Clients keeps the credit visible.
        await AddClient(9203, 0.53m);
        await AddMovement(9203, month, Concept(month), 100m);
        await AddMovement(9203, month, "Pago", 350m, credit: true);
        await balances.RebuildForRentalAsync(9203);
        var credit = await Projected(9203);
        check(credit.StatementTotal == 0m, "credit: projected statement demands no payment");
        check(credit.PreviousBalance == 250m && credit.Balance == 150m && credit.CurrentRent == 100m,
            "credit: previous balance +250 and remaining credit +150 after next rent");
        await AssertMirrorsStatement(9203, 0.53m, "credit");

        // 4. Next month already issued by the monthly job (existing balance row).
        await AddClient(9204, 0.64m);
        await AddMovement(9204, month, Concept(month), 100m);
        await AddMovement(9204, nextMonth, Concept(nextMonth), 120m);
        await balances.RebuildForRentalAsync(9204);
        var issued = await Projected(9204);
        check(issued.CurrentRent == 120m && issued.Balance == -220m && issued.PreviousBalance == -100m,
            "issued next month: rent is the issued debit and balance includes it");
        await AssertMirrorsStatement(9204, 0.64m, "issued next month");

        // 5. Waiting surcharge bag: only the projection anticipates it.
        await AddClient(9205, 0.75m, pendingSurcharge: 25m);
        await AddMovement(9205, month, Concept(month), 100m);
        await balances.RebuildForRentalAsync(9205);
        var bag = await Projected(9205);
        check(bag.InterestAmount == 25m && bag.Balance == -225m && bag.StatementTotal == 225.75m,
            "pending surcharge: projection anticipates the bag once as interest and in the balance");
        await AssertMirrorsStatement(9205, 0.75m, "pending surcharge");

        // 6. Increase falling in the projected month. The rule is the one the operator gets in Finanzas:
        // the percentage configured for that month, rounded UP to the payment method's unit
        // (cash: thousands; every other method: hundreds). No percentage is ever assumed.
        await SetIncreasePercentage(nextMonth, null);
        await AddIncreaseClient(9206, 0.86m, 1000m);
        var unconfigured = await Projected(9206);
        check(unconfigured.CurrentRent == 1000m && unconfigured.IncreasePercentage == null && unconfigured.IncreasePercentageMissing,
            "increase without a configured percentage: current rent is kept and flagged, nothing is guessed");
        await AssertMirrorsStatement(9206, 0.86m, "increase without percentage");

        await SetIncreasePercentage(nextMonth, 6.7m);
        var flagged = await Projected(9206);
        // 1000 + 6.7% = 1067, rounded up to the next hundred.
        check(flagged.CurrentRent == 1100m && flagged.IncreasePercentage == 6.7m && !flagged.IncreasePercentageMissing,
            "increase with a percentage: 1000 + 6.7% rounds up to 1100 and reports the percentage");
        check(flagged.StatementTotal == -flagged.PreviousBalance + flagged.InterestAmount + flagged.CurrentRent + 0.86m,
            "increase: the raised rent is the amount added to the statement total");
        await AssertMirrorsStatement(9206, 0.86m, "increase");

        // The same figures the Angular spec asserts for Finanzas (payment-increase-defaults.integration.spec.ts).
        await AddIncreaseClient(9220, 0.12m, 232000m, CashMethod);
        await AddIncreaseClient(9221, 0.13m, 232000m, BankMethod);
        await AddIncreaseClient(9222, 0.14m, 235000m, CashMethod);
        await AddIncreaseClient(9223, 0.15m, 235000m, BankMethod);
        check((await Projected(9220)).CurrentRent == 248000m && (await Projected(9221)).CurrentRent == 247600m,
            "increase 232000 + 6.7%: cash 248000, bank 247600 (247544 rounds UP to the unit)");
        check((await Projected(9222)).CurrentRent == 251000m && (await Projected(9223)).CurrentRent == 250800m,
            "increase 235000 + 6.7%: cash 251000, bank 250800 as in Finanzas");

        // Rounding goes up, never to the nearest unit: 245410 is closer to 245000 but must be 246000 in cash.
        await AddIncreaseClient(9224, 0.16m, 230000m, CashMethod);
        check((await Projected(9224)).CurrentRent == 246000m, "increase: cash rounds up to 246000, not to the nearest 245000");
        // An exact multiple of the unit does not pay one more step.
        await AddIncreaseClient(9225, 0.17m, 100000m, BankMethod);
        await AddIncreaseClient(9226, 0.18m, 100000m, CashMethod);
        check((await Projected(9225)).CurrentRent == 106700m && (await Projected(9226)).CurrentRent == 107000m,
            "increase 100000 + 6.7%: bank 106700 exactly, cash 107000");
        foreach (var (id, identifier) in new[] { (9220, 0.12m), (9221, 0.13m), (9222, 0.14m), (9223, 0.15m), (9224, 0.16m), (9225, 0.17m), (9226, 0.18m) })
            await AssertMirrorsStatement(id, identifier, $"increase client {id}");

        // An anchor that already passed still owes its increase in the first month charged (Finanzas).
        await AddIncreaseClient(9227, 0.19m, 232000m, CashMethod, anchor: month.AddMonths(-1));
        check((await Projected(9227)).CurrentRent == 248000m, "increase: an overdue anchor still raises the projected month");

        // Cases that must NOT raise the rent.
        await AddIncreaseClient(9228, 0.21m, 232000m, CashMethod, anchor: nextMonth.AddMonths(1));
        var notDue = await Projected(9228);
        check(notDue.CurrentRent == 232000m && notDue.IncreasePercentage == null && !notDue.IncreasePercentageMissing,
            "increase: anchor after the projected month changes nothing and flags nothing");
        await AddIncreaseClient(9229, 0.22m, 232000m, CashMethod, priceLockEnd: nextMonth.AddMonths(2));
        check((await Projected(9229)).CurrentRent == 232000m, "increase: a frozen price is not raised");
        await AddIncreaseClient(9230, 0.23m, 232000m, CashMethod, departure: "SE_VA");
        check((await Projected(9230)).CurrentRent == 232000m, "increase: a client who is leaving (SE VA) is not raised");

        // An amount already planned for the projected month carries its own increase: do not raise it twice.
        await AddIncreaseClient(9231, 0.24m, 230000m, CashMethod);
        await db.ExecuteCommandAsync("""
            UPDATE rental_amount_history SET end_date = @end WHERE rental_id = 9231;
            INSERT rental_amount_history(rental_id, amount, start_date) VALUES(9231, 260000, @start);
            """, [new("@end", nextMonth.AddDays(-1)), new("@start", nextMonth)]);
        var planned = await Projected(9231);
        check(planned.CurrentRent == 260000m && planned.IncreasePercentage == null,
            "increase: an amount planned for the projected month is used as is");
        await AssertMirrorsStatement(9231, 0.24m, "planned amount");

        // The same rule, without a database.
        check(RentIncreaseRule.Apply(100000m, 10m, "Efectivo") == 110000m && RentIncreaseRule.Apply(100000m, 10m, "MP") == 110000m,
            "rule: an exact result is not pushed to the next unit");
        check(RentIncreaseRule.Apply(232000m, 6.7m, "EFECTIVO") == 248000m && RentIncreaseRule.Apply(232000m, 6.7m, null) == 247600m,
            "rule: cash is detected without case and no method means hundreds");
        check(RentIncreaseRule.Apply(232000m, 0m, "Efectivo") == 232000m && RentIncreaseRule.Apply(0m, 10m, "Efectivo") == 0m
              && RentIncreaseRule.Apply(232000m, -3m, "Efectivo") == 232000m,
            "rule: without a positive percentage or base the amount is untouched");

        // 7. Posted interest is shown as interest, not folded into the rent.
        await AddClient(9207, 0.97m);
        await AddMovement(9207, month.AddMonths(-1), "Interés por mora anterior", 10m);
        await AddMovement(9207, month, Concept(month), 100m);
        await balances.RebuildForRentalAsync(9207);
        var interest = await Projected(9207);
        check(interest.InterestAmount == 10m && interest.CurrentRent == 100m,
            "posted interest: shown as interest and rent stays the plain rent");
        await AssertMirrorsStatement(9207, 0.97m, "posted interest");


        // 8. Partial payment of the current month with no next-month debit yet. Clients shows exactly what the
        // projected statement sends. Known and NOT corrected here: that statement keeps the whole current rent as
        // previous balance (200.11 instead of 60 + 100 + identifier) because this branch ignores what was already paid.
        await AddClient(9211, 0.11m);
        await AddMovement(9211, month, Concept(month), 100m);
        await AddMovement(9211, month, "Pago parcial", 40m, credit: true);
        await balances.RebuildForRentalAsync(9211);
        var partial = await Projected(9211);
        check((await statements.GetClientFinancialData(9211)).CurrentBalance == 60.11m,
            "partial payment: the normal statement asks for the 60 still owed");
        check(partial.CurrentRent == 100m && partial.StatementTotal == -partial.PreviousBalance + partial.CurrentRent + 0.11m,
            "partial payment: Clients mirrors the projected statement total");
        await AssertMirrorsStatement(9211, 0.11m, "partial payment");

        // 9. Not projected: inactive client, closed rental, deleted client.
        await AddClient(9208, 0m, clientActive: false);
        await AddClient(9209, 0m, rentalActive: false);
        await AddClient(9210, 0m, deleted: true);
        var result = await service.GetProjectedNextMonthAsync();
        check(!result.Items.Any(item => item.ClientId is 9208 or 9209 or 9210) && !result.FailedClientIds.Any(),
            "inactive clients, closed rentals and deleted clients are not projected");
        check(result.Items.Select(item => item.ClientId).Distinct().Count() == result.Items.Count
              && new[] { 9201, 9202, 9203, 9204, 9205, 9206, 9207, 9211 }.All(id => result.Items.Any(item => item.ClientId == id)),
            "every active client with an active rental appears exactly once");
        check(result.TargetMonth == nextMonth, "target month is the next Argentine calendar month");

        // 10. The bounded parallel batch gives the same numbers as asking one by one.
        foreach (var item in result.Items)
        {
            var statement = await statements.GetClientFinancialData(item.ClientId, isNextMonth: true);
            check(item.StatementTotal == statement.CurrentBalance && item.CurrentRent == statement.Rent,
                $"batch row {item.ClientId} equals the single projected statement");
        }

        // 11. Reading the projection never writes.
        async Task<string> Fingerprint()
        {
            var parts = new List<string>();
            foreach (var sql in new[]
            {
                "SELECT COUNT(*), ISNULL(SUM(amount),0) FROM account_movements",
                "SELECT COUNT(*), ISNULL(SUM(balance+paid+advanced_payment),0) FROM client_month_balances",
                "SELECT COUNT(*), ISNULL(SUM(pending_surcharge),0) FROM rentals"
            })
            {
                var row = (await db.GetTableAsync("fingerprint", sql)).Rows[0];
                parts.Add(string.Join(",", row.ItemArray.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))));
            }
            return string.Join("|", parts);
        }
        var before = await Fingerprint();
        await service.GetProjectedNextMonthAsync();
        check(before == await Fingerprint(), "projecting does not change movements, monthly balances or the surcharge bag");

        await SetIncreasePercentage(nextMonth, null);
    }
}
