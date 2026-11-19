using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using Microsoft.Data.SqlClient;

internal static class StatementSurchargeChecks
{
    public static async Task RunAsync(AccessDB db, ClientMonthBalanceService balances, Action<bool, string> check)
    {
        var now = DateTime.UtcNow.AddHours(-3);
        var month = new DateTime(now.Year, now.Month, 1);
        var nextMonth = month.AddMonths(1);
        const decimal identifier = 7.87m;
        var statements = new CommunicationDao(db);

        async Task<decimal> Scalar(string sql, params SqlParameter[] parameters) =>
            Convert.ToDecimal(await db.ExecuteScalarAsync(sql, parameters));

        async Task AddClient(int id, DateTime pendingPeriod)
        {
            await db.ExecuteCommandAsync("""
                INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id,
                    initial_amount, active, increase_frequency_months, registration_date, receive_communications)
                VALUES(@id, 'Estado recargo prueba', @identifier, 1, 100, 1, 4, @month, 1);
                SET IDENTITY_INSERT rentals ON;
                INSERT rentals(rental_id, client_id, start_date, active, months_unpaid, pending_surcharge,
                    pending_surcharge_rent_base, pending_surcharge_period)
                VALUES(@id, @id, @month, 1, 1, 25, 100, @period);
                SET IDENTITY_INSERT rentals OFF;
                INSERT rental_amount_history(rental_id, amount, start_date) VALUES(@id, 100, @month);
                """, [new("@id", id), new("@identifier", identifier), new("@month", month.AddMonths(-1)), new("@period", pendingPeriod)]);
        }

        async Task AddMovement(int id, DateTime date, string concept, decimal amount, bool credit = false)
        {
            await db.ExecuteCommandAsync("""
                INSERT account_movements(rental_id, movement_date, movement_type, concept, amount)
                VALUES(@id, @date, @type, @concept, @amount);
                """, [new("@id", id), new("@date", date), new("@type", credit ? "CREDITO" : "DEBITO"),
                    new("@concept", concept), new("@amount", amount)]);
        }

        async Task AssertMatchesClients(int id, string scenario)
        {
            var statement = await statements.GetClientFinancialData(id);
            var clients = await new DaoClient(db).GetClients();
            var client = clients.Select($"client_id={id}").Single();
            var postedDebt = Math.Max(-Convert.ToDecimal(client["balance"]), 0m);
            check(statement.Surcharge == Convert.ToDecimal(client["interest_amount"])
                  && statement.PreviousBalance == -Convert.ToDecimal(client["PreviousBalance"])
                  && statement.CurrentBalance == (postedDebt > 0m ? postedDebt + identifier : 0m),
                $"normal statement agrees with Clients: {scenario}");
        }

        // Ordinary account: posted interest remains visible; the waiting bag does not.
        await AddClient(9101, month);
        await AddMovement(9101, month.AddMonths(-1), "Interés por mora anterior", 10m);
        await AddMovement(9101, month, "Alquiler del mes", 100m);
        await balances.RebuildForRentalAsync(9101);
        var normal = await statements.GetClientFinancialData(9101);
        check(normal.Surcharge == 10m && normal.CurrentBalance == 110m + identifier,
            "ordinary statement includes posted interest but excludes the waiting surcharge");
        await AssertMatchesClients(9101, "posted interest and pending surcharge");
        var projected = await statements.GetClientFinancialData(9101, isNextMonth: true);
        check(projected.Surcharge == 35m && projected.CurrentBalance == 235m + identifier,
            "next-month simulation retains posted interest and anticipates the waiting surcharge once");
        check(await Scalar("SELECT pending_surcharge FROM rentals WHERE rental_id=9101") == 25m
              && await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=9101") == 2m,
            "reading normal and projected statements does not materialize or consume the waiting surcharge");

        await db.ExecuteCommandAsync("""
            UPDATE rentals SET pending_surcharge=40, pending_surcharge_period=@future WHERE rental_id=9101;
            """, [new("@future", nextMonth)]);
        var changedBag = await statements.GetClientFinancialData(9101);
        check(changedBag.Surcharge == normal.Surcharge && changedBag.CurrentBalance == normal.CurrentBalance
              && changedBag.PreviousBalance == normal.PreviousBalance,
            "changing a waiting surcharge or its period cannot change the normal statement");
        await AssertMatchesClients(9101, "changed waiting amount and period");

        // Pending fees cannot manufacture current debt on settled or credit accounts.
        await AddClient(9102, month);
        await AddMovement(9102, month, "Alquiler del mes", 100m);
        await AddMovement(9102, month.AddDays(5), "Pago", 100m, credit: true);
        await balances.RebuildForRentalAsync(9102);
        var settled = await statements.GetClientFinancialData(9102);
        check(settled.Surcharge == 0m && settled.CurrentBalance == 0m,
            "waiting surcharge does not create an amount due or payment identifier on a settled account");
        await AssertMatchesClients(9102, "settled account with waiting surcharge");

        await AddClient(9103, month);
        await AddMovement(9103, month, "Alquiler del mes", 100m);
        await AddMovement(9103, month.AddDays(5), "Pago", 150m, credit: true);
        await balances.RebuildForRentalAsync(9103);
        var credit = await statements.GetClientFinancialData(9103);
        check(credit.PreviousBalance == -50m && credit.Surcharge == 0m && credit.CurrentBalance == 0m,
            "normal statement preserves credit without offsetting it against a waiting surcharge");
        await AssertMatchesClients(9103, "credit account with waiting surcharge");
        var creditProjection = await statements.GetClientFinancialData(9103, isNextMonth: true);
        check(creditProjection.Surcharge == 25m && creditProjection.CurrentBalance == 75m + identifier,
            "next-month projection offsets existing credit against projected rent and waiting surcharge");

        // Materialized future interest replaces the bag; projected delivery must stay unchanged.
        await AddClient(9104, month);
        await AddMovement(9104, month, "Alquiler del mes", 100m);
        await AddMovement(9104, month.AddDays(5), "Pago", 100m, credit: true);
        await AddMovement(9104, nextMonth, $"Alquiler {nextMonth:MM/yyyy} (Planificado)", 100m);
        await balances.RebuildForRentalAsync(9104);
        var beforeMaterialization = await statements.GetClientFinancialData(9104, isNextMonth: true);
        await AssertMatchesClients(9104, "future rental and waiting surcharge");
        await db.ExecuteCommandAsync("""
            BEGIN TRANSACTION;
            INSERT account_movements(rental_id, movement_date, movement_type, concept, amount)
            SELECT rental_id, @nextMonth, 'DEBITO', 'Interés por mora del mes anterior', pending_surcharge
            FROM rentals WHERE rental_id=9104;
            UPDATE rentals SET pending_surcharge=0, pending_surcharge_rent_base=NULL, pending_surcharge_period=NULL
            WHERE rental_id=9104;
            COMMIT TRANSACTION;
            """, [new("@nextMonth", nextMonth)]);
        await balances.RebuildForRentalAsync(9104);
        var posted = await statements.GetClientFinancialData(9104);
        var postedProjection = await statements.GetClientFinancialData(9104, isNextMonth: true);
        check(posted.Surcharge == 25m && posted.CurrentBalance == 125m + identifier,
            "normal statement includes surcharge after it becomes a posted debit");
        check(postedProjection.Surcharge == 25m && postedProjection.CurrentBalance == 125m + identifier
              && postedProjection.Surcharge == beforeMaterialization.Surcharge
              && postedProjection.CurrentBalance == beforeMaterialization.CurrentBalance,
            "projected surcharge and total stay unchanged when the bag becomes a next-month debit");
        await AssertMatchesClients(9104, "materialized next-month interest");

        // Exercise the real monthly materialization DAO with a prior-month waiting bag.
        await db.ExecuteCommandAsync("UPDATE rentals SET active=0 WHERE rental_id IN (9101,9102,9103,9104); UPDATE clients SET active=0 WHERE client_id IN (9101,9102,9103,9104);");
        await AddClient(9105, month.AddMonths(-1));
        await AddMovement(9105, month.AddMonths(-1), "Alquiler mes anterior", 100m);
        await AddMovement(9105, month, "Alquiler del mes", 100m);
        await balances.RebuildForRentalAsync(9105);
        var beforeMonthlyJob = await statements.GetClientFinancialData(9105);
        check(beforeMonthlyJob.Surcharge == 0m && beforeMonthlyJob.CurrentBalance == 200m + identifier,
            "overdue waiting bag is still excluded until the monthly debit is actually posted");
        var rentals = new DaoRental(db);
        await rentals.ApplyPendingSurchargesAsync();
        await balances.RebuildAllActiveRentalsAsync();
        var afterMonthlyJob = await statements.GetClientFinancialData(9105);
        check(afterMonthlyJob.Surcharge == 25m && afterMonthlyJob.CurrentBalance == 225m + identifier
              && await Scalar("SELECT pending_surcharge FROM rentals WHERE rental_id=9105") == 0m,
            "monthly materialization moves the waiting bag into posted interest and statement total once");
        await AssertMatchesClients(9105, "monthly materialization");
        await rentals.ApplyPendingSurchargesAsync();
        await balances.RebuildAllActiveRentalsAsync();
        var repeatedMonthlyJob = await statements.GetClientFinancialData(9105);
        check(repeatedMonthlyJob.Surcharge == afterMonthlyJob.Surcharge
              && repeatedMonthlyJob.CurrentBalance == afterMonthlyJob.CurrentBalance
              && await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=9105 AND concept LIKE 'Interés por mora%'") == 1m,
            "rerunning monthly materialization after the bag is cleared does not duplicate the statement interest");
    }
}
