using System.Globalization;
using System.Reflection;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Services.accountMovement;
using GuardeSoftwareAPI.Services.activityLog;
using GuardeSoftwareAPI.Services.client;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using GuardeSoftwareAPI.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

internal static class DepartureChecks
{
    public static async Task RunAsync(AccessDB db, ClientMonthBalanceService balances, Action<bool, string> check)
    {
        await db.ExecuteCommandAsync("ALTER TABLE lockers ADD status VARCHAR(50); INSERT payment_methods VALUES(90, 'Efectivo', 0, 1), (91, 'Banco', 0, 1);");
        var activity = DispatchProxy.Create<IActivityLogService, DepartureActivityStub>();
        var history = new RentalAmountHistoryService(db, activity);
        var movements = new AccountMovementService(db, NullLogger<AccountMovementService>.Instance, balances, null!, history);
        var service = new ClientService(db, NullLogger<ClientService>.Instance, movements, null!, history, null!, activity, null!, null!, null!, balances);
        var today = TimeHelper.GetArgentinaTime().Date;
        var month = new DateTime(today.Year, today.Month, 1);
        var culture = new CultureInfo("es-AR");
        string Concept(DateTime date) => $"Alquiler {culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(date.Month))} {date.Year}";
        async Task<decimal> Scalar(string sql) => Convert.ToDecimal(await db.ExecuteScalarAsync(sql));

        async Task Reset(int paymentMethod = 90)
        {
            await db.ExecuteCommandAsync("""
                DELETE client_month_balances WHERE rental_id=90; DELETE account_movements WHERE rental_id=90;
                DELETE rental_amount_history WHERE rental_id=90; DELETE rentals WHERE rental_id=90; DELETE clients WHERE client_id=90;
                INSERT clients(client_id, preferred_payment_method_id, active, is_deleted, full_name, initial_amount)
                VALUES(90, @method, 1, 0, 'Cliente retiro prueba', 123456);
                SET IDENTITY_INSERT rentals ON; INSERT rentals(rental_id, client_id, start_date, active, pending_surcharge, increase_anchor_date)
                VALUES(90, 90, '2020-01-01', 1, 0, NULL);
                INSERT rental_amount_history(rental_id, amount, start_date) VALUES(90, 123456, '2020-01-01');
                """, [new SqlParameter("@method", paymentMethod)]);
        }
        async Task AddDebit(DateTime date, decimal amount, int? paymentId = null)
        {
            await db.ExecuteCommandAsync("""
                INSERT account_movements(rental_id, movement_date, movement_type, concept, amount, payment_id)
                VALUES(90, @date, 'DEBITO', @concept, @amount, @payment);
                """, [new SqlParameter("@date", date), new SqlParameter("@concept", Concept(date)),
                    new SqlParameter("@amount", amount), new SqlParameter("@payment", (object?)paymentId ?? DBNull.Value)]);
        }
        ClientDepartureActionDto Request(DateTime date, decimal? amount = null) => new()
        {
            Action = "SE_VA", ChargeProportional = true, DepartureDate = date, ProportionalAmount = amount
        };

        foreach (var method in new[] { 90, 91 })
        {
            await Reset(method);
            foreach (var date in new[] { month.AddDays(15), month.AddMonths(3).AddDays(10), new DateTime(2024, 2, 29) })
            {
                var preview = await service.GetDepartureProportionalPreviewAsync(90, date);
                var step = method == 90 ? 1000m : 100m;
                var expected = Math.Round(123456m / DateTime.DaysInMonth(date.Year, date.Month) * date.Day / step, MidpointRounding.AwayFromZero) * step;
                check(preview.ProportionalAmount == expected && preview.DaysInMonth == DateTime.DaysInMonth(date.Year, date.Month),
                    $"departure preview accepts {date:yyyy-MM-dd} and preserves rounding for method {method}");
            }
        }

        await Reset();
        await AddDebit(month, 123456, 900);
        var originalId = await Scalar("SELECT movement_id FROM account_movements WHERE rental_id=90");
        await AddDebit(month.AddMonths(1), 123456);
        await db.ExecuteCommandAsync("""
            INSERT account_movements(rental_id, movement_date, movement_type, concept, amount, payment_id)
            VALUES(90, @date, 'CREDITO', 'Pago ya registrado', 50000, 900);
            """, [new SqlParameter("@date", month.AddDays(4))]);
        await service.ApplyDepartureActionAsync(90, Request(month.AddDays(15), 43210.55m));
        check(await Scalar($"SELECT amount FROM account_movements WHERE movement_id={originalId}") == 43210.55m &&
              await Scalar($"SELECT payment_id FROM account_movements WHERE movement_id={originalId}") == 900 &&
              Convert.ToDateTime(await db.ExecuteScalarAsync($"SELECT movement_date FROM account_movements WHERE movement_id={originalId}")) == month,
            "current departure updates debit in place and keeps exact manual amount, date and payment link");
        check(await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=90 AND movement_type='DEBITO'") == 2 &&
              await Scalar("SELECT SUM(amount) FROM account_movements WHERE rental_id=90 AND movement_type='CREDITO'") == 50000,
            "departure preserves next month debit and recorded credits unless removal is requested");
        check(await Scalar("SELECT SUM(monthly_debits) FROM client_month_balances WHERE rental_id=90") == 166666.55m &&
              await Scalar("SELECT SUM(allocated_rent) FROM client_month_balances WHERE rental_id=90") == 50000,
            "departure rebuild uses the shared payment allocation engine");
        await service.ApplyDepartureActionAsync(90, Request(month.AddDays(16), 40000m));
        check(await Scalar($"SELECT amount FROM account_movements WHERE movement_id={originalId}") == 40000m &&
              await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=90 AND movement_type='DEBITO'") == 2,
            "repeated departure confirmation updates the same debit without duplicates");
        await service.ApplyDepartureActionAsync(90, new() { Action = "SE_QUEDA", RestoreProportional = true });
        check(await Scalar($"SELECT amount FROM account_movements WHERE rental_id=90 AND concept='{Concept(month)}'") == 123456m,
            "SE QUEDA restores the full debit for the actual departure month");

        await Reset(91);
        var future = month.AddMonths(4).AddDays(14);
        var futurePreview = await service.GetDepartureProportionalPreviewAsync(90, future);
        await service.ApplyDepartureActionAsync(90, Request(future));
        check(await Scalar("SELECT amount FROM account_movements WHERE rental_id=90") == futurePreview.ProportionalAmount &&
              Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT movement_date FROM account_movements WHERE rental_id=90")) == new DateTime(future.Year, future.Month, 1),
            "missing future debit is created once in its own month with the suggested rounded amount");
        await service.ApplyDepartureActionAsync(90, new() { Action = "SE_QUEDA", RestoreProportional = false });
        check(await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=90") == 1,
            "SE QUEDA can retain the proportional without adding a full debit");

        await Reset();
        await AddDebit(month, 123456);
        await service.ApplyDepartureActionAsync(90, Request(month, 0));
        check(await Scalar("SELECT amount FROM account_movements WHERE rental_id=90") == 0,
            "manual zero replaces the existing debit");
        foreach (var invalidAmount in new[] { -1m, 1.234m })
        {
            await Reset();
            await AddDebit(month, 123456);
            bool rejected = false;
            try { await service.ApplyDepartureActionAsync(90, Request(month, invalidAmount)); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && await Scalar("SELECT amount FROM account_movements WHERE rental_id=90") == 123456,
                "invalid departure amount leaves ledger unchanged");
        }

        await Reset();
        await AddDebit(month, 123456);
        await AddDebit(month, 123456);
        bool duplicateRejected = false;
        try { await service.ApplyDepartureActionAsync(90, Request(month, 1000)); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        check(duplicateRejected && await Scalar("SELECT SUM(amount) FROM account_movements WHERE rental_id=90") == 246912,
            "ambiguous duplicate debit rejects the complete transaction");

        await Reset();
        await AddDebit(month, 123456);
        ((DepartureActivityStub)(object)activity).RejectTransaction = true;
        bool rolledBack = false;
        try { await service.ApplyDepartureActionAsync(90, Request(month, 1000)); }
        catch (InvalidOperationException) { rolledBack = true; }
        check(rolledBack && await Scalar("SELECT amount FROM account_movements WHERE rental_id=90") == 123456 &&
              await db.ExecuteScalarAsync("SELECT departure_status FROM clients WHERE client_id=90") == DBNull.Value,
            "departure rolls back debit, balance and status when audit persistence fails");
    }
}

public class DepartureActivityStub : DispatchProxy
{
    public bool RejectTransaction { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name is "CreateActivityLogTransactionAsync" or "TryCreateActivityLogAsync")
        {
            if (RejectTransaction) throw new InvalidOperationException("Test audit persistence failure");
            return Task.FromResult(true);
        }
        throw new NotSupportedException(method?.Name);
    }
}
