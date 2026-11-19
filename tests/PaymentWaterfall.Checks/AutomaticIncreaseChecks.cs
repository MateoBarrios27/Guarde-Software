using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Jobs;
using GuardeSoftwareAPI.Services.accountMovement;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

// Automatic rent increases must round like the increase the operator confirms in Finanzas:
// the amount goes UP to the unit of the client's payment method (cash: thousands, any other
// method: hundreds). Covers the monthly debit job (missed increase) and ApplyMonthlyIncreasesJob.
internal static class AutomaticIncreaseChecks
{
    public static async Task RunAsync(AccessDB db, ClientMonthBalanceService balances, AccountMovementService monthlyDebits,
        IRentalAmountHistoryService historyService, Action<bool, string> check)
    {
        // Both jobs run on the server clock, like the production jobs.
        var month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var previousMonth = month.AddMonths(-1);
        const int CashMethod = 9301;
        const int BankMethod = 9302;
        const decimal Percentage = 6.7m;

        // The harness seeds Ariadna (rental 1) for the waterfall suite; keep her out of the jobs.
        await db.ExecuteCommandAsync("UPDATE rentals SET active = 0 WHERE rental_id = 1");
        await db.ExecuteCommandAsync("INSERT payment_methods VALUES(9301, 'Efectivo', 0, 1), (9302, 'Transferencia', 0, 1);");

        async Task SetPercentage(DateTime effectiveMonth, decimal? percentage)
        {
            await db.ExecuteCommandAsync("DELETE FROM monthly_increase_settings WHERE effective_date = @month",
                [new("@month", effectiveMonth)]);
            if (percentage.HasValue)
                await db.ExecuteCommandAsync("INSERT monthly_increase_settings(effective_date, percentage) VALUES(@month, @percentage)",
                    [new("@month", effectiveMonth), new("@percentage", percentage.Value)]);
        }

        async Task AddRental(int id, int paymentMethodId, decimal rent, DateTime anchor, bool previousMonthUnpaid)
        {
            await db.ExecuteCommandAsync("""
                INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount,
                    active, is_deleted, increase_frequency_months, registration_date, receive_communications)
                VALUES(@id, 'Aumento automático prueba', 0, @method, @rent, 1, 0, 4, @start, 1);
                SET IDENTITY_INSERT rentals ON;
                INSERT rentals(rental_id, client_id, start_date, active, months_unpaid, pending_surcharge, increase_anchor_date)
                VALUES(@id, @id, @start, 1, 0, 0, @anchor);
                SET IDENTITY_INSERT rentals OFF;
                INSERT rental_amount_history(rental_id, amount, start_date) VALUES(@id, @rent, @historyStart);
                """, [new("@id", id), new("@method", paymentMethodId), new("@rent", rent), new("@start", previousMonth),
                    new("@anchor", anchor), new("@historyStart", month.AddYears(-2))]);
            if (previousMonthUnpaid)
            {
                await db.ExecuteCommandAsync("""
                    INSERT account_movements(rental_id, movement_date, movement_type, concept, amount)
                    VALUES(@id, @previous, 'DEBITO', 'Alquiler mes anterior', @rent);
                    """, [new("@id", id), new("@previous", previousMonth), new("@rent", rent)]);
                await balances.RebuildForRentalAsync(id);
            }
        }

        async Task<decimal> LatestRent(int id) => Convert.ToDecimal(await db.ExecuteScalarAsync(
            "SELECT TOP 1 amount FROM rental_amount_history WHERE rental_id = @id ORDER BY start_date DESC, rental_amount_history_id DESC",
            [new SqlParameter("@id", id)]));

        async Task<decimal> DebitOfMonth(int id) => Convert.ToDecimal(await db.ExecuteScalarAsync(
            "SELECT amount FROM account_movements WHERE rental_id = @id AND movement_date >= @month AND concept LIKE 'Alquiler %'",
            [new SqlParameter("@id", id), new SqlParameter("@month", month)]));

        async Task<DateTime> Anchor(int id) => Convert.ToDateTime(await db.ExecuteScalarAsync(
            "SELECT increase_anchor_date FROM rentals WHERE rental_id = @id", [new SqlParameter("@id", id)]));

        // 1. ApplyMonthlyIncreasesJob (the job that raises the rent on its anchor date).
        await SetPercentage(month, Percentage);
        await SetPercentage(month.AddMonths(-4), 5m);
        await AddRental(9401, BankMethod, 232000m, month, previousMonthUnpaid: false);
        await AddRental(9402, CashMethod, 232000m, month, previousMonthUnpaid: false);
        await AddRental(9403, CashMethod, 230000m, month, previousMonthUnpaid: false);
        await AddRental(9404, BankMethod, 100000m, month, previousMonthUnpaid: false);
        // Two increases owed at once: each stage is rounded and is the base of the next, as in Finanzas.
        await AddRental(9405, CashMethod, 232000m, month.AddMonths(-4), previousMonthUnpaid: false);

        var increaseJob = new ApplyMonthlyIncreasesJob(NullLogger<ApplyMonthlyIncreasesJob>.Instance, db, historyService);
        await increaseJob.Execute(null!);

        check(await LatestRent(9401) == 247600m, "increase job: 232000 + 6.7% by transfer rounds up to 247600, not 248000");
        check(await LatestRent(9402) == 248000m, "increase job: 232000 + 6.7% in cash is 248000");
        check(await LatestRent(9403) == 246000m, "increase job: 230000 + 6.7% in cash rounds up to 246000, not 245000");
        check(await LatestRent(9404) == 106700m, "increase job: an exact result (100000 + 6.7% by transfer) is 106700, not 107000");
        // 232000 + 5% = 243600 -> 244000; + 6.7% = 260348 -> 261000 (rounding only once at the end would give 260000).
        check(await LatestRent(9405) == 261000m, "increase job: successive increases round up at every stage like Finanzas");
        check(await Anchor(9401) == month.AddMonths(4) && await Anchor(9405) == month.AddMonths(4),
            "increase job: the anchor moves by the client's frequency");

        // 2. Monthly debit job: the missed increase (previous month completely unpaid, no planned debit).
        await AddRental(9411, BankMethod, 232000m, month, previousMonthUnpaid: true);
        await AddRental(9412, CashMethod, 232000m, month, previousMonthUnpaid: true);
        await AddRental(9413, CashMethod, 230000m, month, previousMonthUnpaid: true);
        await AddRental(9414, BankMethod, 100000m, month, previousMonthUnpaid: true);
        await monthlyDebits.ApplyMonthlyDebitsAsync();

        check(await DebitOfMonth(9411) == 247600m && await LatestRent(9411) == 247600m,
            "monthly debit: 232000 + 6.7% by transfer is debited and recorded as 247600, not 248000");
        check(await DebitOfMonth(9412) == 248000m, "monthly debit: 232000 + 6.7% in cash is 248000");
        check(await DebitOfMonth(9413) == 246000m && await LatestRent(9413) == 246000m,
            "monthly debit: 230000 + 6.7% in cash rounds up to 246000, not 245000");
        check(await DebitOfMonth(9414) == 106700m, "monthly debit: an exact result (100000 + 6.7% by transfer) is 106700, not 107000");
        check(await Anchor(9411) == month.AddMonths(3), "monthly debit: the anchor advances by frequency - 1 months");

        // 3. Without a percentage configured nothing is assumed: the current rent is debited.
        await SetPercentage(month, null);
        await AddRental(9421, CashMethod, 232000m, month, previousMonthUnpaid: true);
        await monthlyDebits.ApplyMonthlyDebitsAsync();
        check(await DebitOfMonth(9421) == 232000m && await LatestRent(9421) == 232000m,
            "monthly debit: a due increase without a configured percentage keeps the current rent");

        await SetPercentage(month, null);
        await SetPercentage(month.AddMonths(-4), null);
    }
}
