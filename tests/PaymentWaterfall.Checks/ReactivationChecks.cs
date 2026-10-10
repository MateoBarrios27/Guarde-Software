using System.Reflection;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.AccountMovement;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Services.accountMovement;
using GuardeSoftwareAPI.Services.activityLog;
using GuardeSoftwareAPI.Services.address;
using GuardeSoftwareAPI.Services.client;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Services.email;
using GuardeSoftwareAPI.Services.phone;
using GuardeSoftwareAPI.Services.rental;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using GuardeSoftwareAPI.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ReactivationChecks
{
    public static async Task RunAsync(AccessDB db, ClientMonthBalanceService balances, Action<bool, string> check)
    {
        var audit = DispatchProxy.Create<IActivityLogService, ReactivationExternalStub>();
        var history = new RentalAmountHistoryService(db, audit);
        var movements = new AccountMovementService(db, NullLogger<AccountMovementService>.Instance, balances, null!, history);
        var service = new ClientService(db, NullLogger<ClientService>.Instance, movements, new RentalService(db), history,
            null!, audit, DispatchProxy.Create<IEmailService, ReactivationExternalStub>(),
            DispatchProxy.Create<IPhoneService, ReactivationExternalStub>(),
            DispatchProxy.Create<IAddressService, ReactivationExternalStub>(), balances);
        var today = TimeHelper.GetArgentinaTime();
        var month = new DateTime(today.Year, today.Month, 1);
        async Task<decimal> Scalar(string sql) => Convert.ToDecimal(await db.ExecuteScalarAsync(sql));
        async Task Reset(decimal debt = 0, decimal credit = 0, decimal interest = 0, decimal pending = 0)
        {
            ((ReactivationExternalStub)(object)audit).RejectAudit = false;
            await db.ExecuteCommandAsync("""
                DELETE client_month_balances WHERE rental_id IN (SELECT rental_id FROM rentals WHERE client_id=100);
                DELETE account_movements WHERE rental_id IN (SELECT rental_id FROM rentals WHERE client_id=100);
                DELETE rental_amount_history WHERE rental_id IN (SELECT rental_id FROM rentals WHERE client_id=100);
                DELETE rentals WHERE client_id=100; DELETE clients WHERE client_id=100;
                INSERT clients(client_id, full_name, payment_identifier, registration_date, preferred_payment_method_id,
                    initial_amount, active, increase_frequency_months, receive_communications)
                VALUES(100, 'Cliente reactivación prueba', 99.99, @start, 90, 100000, 0, 4, 0);
                SET IDENTITY_INSERT rentals ON;
                INSERT rentals(rental_id, client_id, start_date, end_date, active, pending_surcharge, months_unpaid)
                VALUES(100, 100, @start, @end, 0, @pending, 1);
                INSERT rental_amount_history(rental_id, amount, start_date, end_date) VALUES(100, 100000, @start, @end);
                """, [new SqlParameter("@start", today.AddYears(-2)), new SqlParameter("@end", month.AddDays(-1)), new SqlParameter("@pending", pending)]);
            async Task Insert(string type, string concept, decimal amount, DateTime date)
            {
                if (amount <= 0) return;
                await db.ExecuteCommandAsync("""
                    INSERT account_movements(rental_id, movement_date, movement_type, concept, amount, payment_id)
                    VALUES(100, @date, @type, @concept, @amount, NULL);
                    """, [new SqlParameter("@date", date), new SqlParameter("@type", type), new SqlParameter("@concept", concept), new SqlParameter("@amount", amount)]);
            }
            await Insert("DEBITO", "Alquiler anterior", debt, month.AddMonths(-1));
            await Insert("DEBITO", "Interés por mora anterior", interest, month.AddMonths(-1));
            await Insert("CREDITO", "Pago anterior", credit, month.AddDays(1));
            await balances.RebuildForRentalAsync(100);
        }
        CreateClientDTO Request(ClientReactivationContextDto context, string action) => new()
        {
            FullName = "Cliente reactivación prueba", UserID = 1, PaymentIdentifier = 99.99m,
            PreferredPaymentMethodId = 90, Amount = 100000, ReactivationBalanceAction = action,
            ExpectedReactivationRentalId = context.RentalId, ExpectedReactivationBalance = context.Balance
        };
        async Task<int> ActiveRental() => Convert.ToInt32(await db.ExecuteScalarAsync("SELECT rental_id FROM rentals WHERE client_id=100 AND active=1"));
        async Task<decimal> Net(int id) => await Scalar($"SELECT ISNULL(SUM(CASE WHEN movement_type='DEBITO' THEN amount ELSE -amount END),0) FROM account_movements WHERE rental_id={id}");
        async Task<decimal> NewRent(int id) => await Scalar($"SELECT SUM(amount) FROM account_movements WHERE rental_id={id} AND movement_type='DEBITO' AND concept LIKE 'Alquiler %'");

        await Reset(100000);
        var beforeCount = await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=100");
        var manual = await movements.CreateManualMovementAsync(new CreateAccountMovementDTO
        { ClientId = 100, MovementType = "CREDITO", Concept = "Ajuste manual cliente dado de baja", Amount = 25000, Date = today });
        check(manual.RentalId == 100 && await Net(100) == 75000 &&
            await Scalar("SELECT SUM(allocated_rent) FROM client_month_balances WHERE rental_id=100") == 25000,
            "inactive client can create a manual credit in its latest closed rental and rebuild allocations");
        await movements.CreateManualMovementAsync(new CreateAccountMovementDTO
        { ClientId = 100, MovementType = "DEBITO", Concept = "Cargo manual cliente dado de baja", Amount = 1000, Date = today });
        check(await Net(100) == 76000 && await Scalar("SELECT active FROM clients WHERE client_id=100") == 0,
            "inactive manual debit does not reactivate the client");
        var contextAfterManual = await service.GetReactivationContextAsync(100);
        check(contextAfterManual.Balance == -76000 && contextAfterManual.RentalId == 100 &&
            await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=100") == beforeCount + 2,
            "reactivation context reads the live ledger without changing movements");

        await Reset(100000, 20000, 10000, 5000);
        var context = await service.GetReactivationContextAsync(100);
        check(context.Balance == -95000, "reactivation balance includes unpaid rent, interest and pending surcharge");
        await service.ReactivateClientAsync(100, Request(context, "keep"));
        var active = await ActiveRental();
        check(await Net(100) == 0 && await Net(active) == await NewRent(active) + 95000,
            "keep debt transfers the exact balance to the new rental without double counting");
        check(await Scalar($"SELECT amount FROM account_movements WHERE rental_id={active} AND concept LIKE 'Saldo anterior%'") == 80000 &&
            await Scalar($"SELECT amount FROM account_movements WHERE rental_id={active} AND concept LIKE 'Interés por mora anterior%'") == 15000 &&
            await Scalar("SELECT pending_surcharge FROM rentals WHERE rental_id=100") == 0,
            "transferred principal and interest remain separate and pending surcharge is materialized once");
        check(await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=100 AND concept IN ('Alquiler anterior','Interés por mora anterior','Pago anterior')") == 3,
            "reactivation retains every original debit and credit");
        var detail = await new DaoClient(db).GetClientDetailByIdAsync(100);
        check(Convert.ToDecimal(detail.Rows[0]["balance"]) == -await Net(active),
            "client detail displays the carried balance together with new rent charges");
        var statement = await new CommunicationDao(db).GetClientFinancialData(100);
        check(statement.PreviousBalance == 80000 && statement.Surcharge == 15000 &&
            statement.CurrentBalance == await Net(active) + 99.99m,
            "account statement keeps carried principal and interest separate without losing the previous balance");

        bool duplicateRejected = false;
        try { await service.ReactivateClientAsync(100, Request(context, "keep")); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        check(duplicateRejected && await Scalar("SELECT COUNT(*) FROM rentals WHERE client_id=100 AND active=1") == 1,
            "a repeated reactivation cannot create another active rental");

        foreach (var action in new[] { "keep", "zero" })
        {
            await Reset(credit: 25000);
            context = await service.GetReactivationContextAsync(100);
            check(context.Balance == 25000, "reactivation context distinguishes a credit balance");
            await service.ReactivateClientAsync(100, Request(context, action));
            active = await ActiveRental();
            check(await Net(100) == 0 && await Net(active) == await NewRent(active) - (action == "keep" ? 25000 : 0),
                $"reactivation {action} handles credit balance correctly");
            var creditHistory = await movements.GetAccountMovementListByClientIdAsync(100);
            check(creditHistory.Sum(m => m.MovementType == "CREDITO" ? m.Amount : -m.Amount) == -await Net(active),
                $"reactivation {action} client movement history reconciles old and new rental balances");
            if (action == "zero")
            {
                var adjustment = creditHistory.Single(m => m.Concept == "Ajuste de saldo a cero por reactivación");
                check(adjustment.RentalId == 100 && adjustment.MovementType == "DEBITO" && adjustment.Amount == 25000 &&
                    creditHistory.Any(m => m.Concept == "Pago anterior" && m.Amount == 25000),
                    "zero credit exposes the compensating debit and retains the original credit in client detail history");
            }
        }
        await Reset(100000, 20000, 10000, 5000);
        await service.ReactivateClientAsync(100, Request(await service.GetReactivationContextAsync(100), "zero"));
        active = await ActiveRental();
        check(await Net(100) == 0 && await Net(active) == await NewRent(active) &&
            await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=100 AND concept='Ajuste de saldo a cero por reactivación'") == 1,
            "zero debt writes a compensating adjustment and keeps only new rent charges");
        var zeroHistory = await movements.GetAccountMovementListByClientIdAsync(100);
        var zeroAdjustment = zeroHistory.Single(m => m.Concept == "Ajuste de saldo a cero por reactivación");
        check(zeroAdjustment.RentalId == 100 && zeroAdjustment.MovementType == "CREDITO" && zeroAdjustment.Amount == 95000 &&
            zeroHistory.Count(m => m.RentalId == 100) == 5,
            "zero debt exposes its 95000 compensating credit together with every original movement in client detail history");
        check(zeroHistory.Sum(m => m.MovementType == "CREDITO" ? m.Amount : -m.Amount) == -await NewRent(active),
            "zero debt client history cancels the entire previous balance and leaves only reactivation rent");
        detail = await new DaoClient(db).GetClientDetailByIdAsync(100);
        check(Convert.ToDecimal(detail.Rows[0]["balance"]) == -await NewRent(active),
            "zero debt client summary excludes cancelled previous debt");
        statement = await new CommunicationDao(db).GetClientFinancialData(100);
        check(statement.PreviousBalance == 0 && statement.Surcharge == 0 &&
            statement.CurrentBalance == await NewRent(active) + 99.99m,
            "zero debt account statement excludes cancelled principal, interests and pending surcharge");

        await Reset();
        context = await service.GetReactivationContextAsync(100);
        check(context.Balance == 0, "zero previous balance needs no balance-choice modal");
        await service.ReactivateClientAsync(100, Request(context, "keep"));
        active = await ActiveRental();
        check(await Net(active) == await NewRent(active) && await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=100") == 0,
            "zero previous balance creates no transfer or adjustment");

        await Reset(credit: 5000, pending: 5000);
        context = await service.GetReactivationContextAsync(100);
        check(context.Balance == 0, "pending surcharge fully offset by credit has zero balance");
        await service.ReactivateClientAsync(100, Request(context, "keep"));
        active = await ActiveRental();
        check(await Net(100) == 0 && await Scalar("SELECT pending_surcharge FROM rentals WHERE rental_id=100") == 0 &&
            await Net(active) == await NewRent(active),
            "zero net balance clears the pending surcharge without carrying a hidden debt");

        await Reset(100000);
        context = await service.GetReactivationContextAsync(100);
        await movements.CreateManualMovementAsync(new CreateAccountMovementDTO
        { ClientId = 100, MovementType = "CREDITO", Concept = "Cambio posterior a decisión", Amount = 1000, Date = today });
        bool staleRejected = false;
        try { await service.ReactivateClientAsync(100, Request(context, "zero")); }
        catch (InvalidOperationException) { staleRejected = true; }
        check(staleRejected && await Net(100) == 99000 && await Scalar("SELECT active FROM clients WHERE client_id=100") == 0,
            "a stale balance decision is rejected before reactivating or adjusting the ledger");

        await Reset(100000);
        context = await service.GetReactivationContextAsync(100);
        ((ReactivationExternalStub)(object)audit).RejectAudit = true;
        bool rolledBack = false;
        try { await service.ReactivateClientAsync(100, Request(context, "zero")); }
        catch (InvalidOperationException) { rolledBack = true; }
        check(rolledBack && await Net(100) == 100000 &&
            await Scalar("SELECT active FROM clients WHERE client_id=100") == 0 &&
            await Scalar("SELECT COUNT(*) FROM rentals WHERE client_id=100") == 1,
            "reactivation rolls back balance adjustment and new rental when audit persistence fails");

        await Reset(100000);
        var missingDecision = Request(await service.GetReactivationContextAsync(100), "keep");
        missingDecision.ReactivationBalanceAction = null;
        missingDecision.ExpectedReactivationBalance = null;
        bool decisionRejected = false;
        try { await service.ReactivateClientAsync(100, missingDecision); }
        catch (InvalidOperationException) { decisionRejected = true; }
        check(decisionRejected && await Net(100) == 100000,
            "nonzero balance requires an explicit decision even for a direct API request");
    }
}

public class ReactivationExternalStub : DispatchProxy
{
    public bool RejectAudit { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (RejectAudit && method?.Name == "CreateActivityLogTransactionAsync")
            throw new InvalidOperationException("Test reactivation audit failure");
        if (method?.ReturnType == typeof(Task<bool>)) return Task.FromResult(true);
        if (method?.ReturnType == typeof(Task<int>)) return Task.FromResult(0);
        if (method?.ReturnType == typeof(Task)) return Task.CompletedTask;
        throw new NotSupportedException(method?.Name);
    }
}
