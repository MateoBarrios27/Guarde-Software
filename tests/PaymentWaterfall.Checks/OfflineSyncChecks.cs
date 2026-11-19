using GuardeSoftwareAPI.Controllers;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Sync;
using GuardeSoftwareAPI.Services.payment;
using GuardeSoftwareAPI.Services.sync;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

internal static class OfflineSyncChecks
{
    public static async Task RunAsync(AccessDB db, PaymentService payments, ClientMonthBalanceService balances, Action<bool, string> check)
    {
        var sync = new SyncService(db, payments, NullLogger<SyncService>.Instance);
        var month = new DateTime(DateTime.UtcNow.AddHours(-3).Year, DateTime.UtcNow.AddHours(-3).Month, 1);
        const int clientId = 910;
        await db.ExecuteCommandAsync("""
            INSERT clients(client_id,full_name,payment_identifier,preferred_payment_method_id,initial_amount,active,increase_frequency_months)
            VALUES(910,'Offline prueba',9.10,1,100,1,4);
            SET IDENTITY_INSERT rentals ON;
            INSERT rentals(rental_id,client_id,start_date,active,months_unpaid,pending_surcharge) VALUES(910,910,'2020-01-01',1,0,0);
            SET IDENTITY_INSERT rentals OFF;
            INSERT rental_amount_history(rental_id,amount,start_date) VALUES(910,100,'2020-01-01');
            """);
        async Task Reset()
        {
            await db.ExecuteCommandAsync("""
                DELETE account_movements WHERE rental_id=910;
                DELETE client_month_balances WHERE rental_id=910;
                DELETE payments WHERE client_id=910;
                UPDATE rentals SET pending_surcharge=0,pending_surcharge_period=NULL,price_lock_end_date=NULL WHERE rental_id=910;
                """);
            await db.ExecuteCommandAsync("INSERT account_movements(rental_id,movement_date,movement_type,concept,amount) VALUES(910,@month,'DEBITO',@concept,100)",
                [new SqlParameter("@month", month), new SqlParameter("@concept", $"Alquiler {month:MM/yyyy}")]);
            await balances.RebuildForRentalAsync(clientId);
        }
        async Task<int> Count(string table, string predicate) => Convert.ToInt32(await db.ExecuteScalarAsync($"SELECT COUNT(*) FROM {table} WHERE {predicate}"));
        OfflinePaymentDto Dto(string localId, decimal amount = 100) => new() { LocalId = localId, ClientId = clientId, PaymentMethodId = 1,
            MovementType = "CREDITO", Concept = "Cobro offline de prueba", Amount = amount, Date = month.AddDays(4), SkipFutureProjection = false };
        async Task<SyncPaymentResultDto> Send(OfflinePaymentDto dto) => (await sync.ProcessOfflinePaymentsAsync(new() { Payments = [dto] })).Results.Single();

        await Reset();
        var beforeMigration = await Send(Dto(Guid.NewGuid().ToString()));
        check(!beforeMigration.Success && beforeMigration.ErrorCode == "OFFLINE_SCHEMA_REQUIRED" && await Count("payments", "client_id=910") == 0,
            "offline without migration refuses writes and returns a recoverable schema error");
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "offline-receipts-migration.sql"));
        await db.ExecuteCommandAsync(migration); await db.ExecuteCommandAsync(migration);
        check(true, "offline receipt migration applies twice");

        var localId = Guid.NewGuid().ToString();
        var first = await Send(Dto(localId));
        check(first.Success && first.PaymentId > 0 && await Count("payments", "client_id=910") == 1, "offline exact payment commits once");
        var token = (await new PaymentStateService(db).GetSnapshotAsync(clientId)).Token;
        var replay = await Send(Dto(localId));
        check(replay.Success && replay.PaymentId == first.PaymentId && await Count("payments", "client_id=910") == 1, "lost response replay acknowledges original payment without creating another");
        check((await new PaymentStateService(db).GetSnapshotAsync(clientId)).Token == token, "receipt replay preserves complete financial state");
        check(await Count("offline_payment_receipts", $"local_id='{localId}'") == 1, "payment and receipt have a single durable identity");
        var altered = await Send(Dto(localId, 101));
        check(!altered.Success && altered.ErrorCode == "OFFLINE_PAYMENT_CHANGED" && await Count("payments", "client_id=910") == 1, "same ID with altered amount is rejected without changes");
        var duplicate = await Send(Dto(Guid.NewGuid().ToString()));
        check(!duplicate.Success && duplicate.ErrorCode == "DUPLICATE_PAYMENT", "different local ID retains existing duplicate-payment protection");

        await Reset(); localId = Guid.NewGuid().ToString();
        var concurrent = await Task.WhenAll(Send(Dto(localId)), Send(Dto(localId)));
        check(concurrent.All(r => r.Success) && concurrent.Select(r => r.PaymentId).Distinct().Count() == 1 && await Count("payments", "client_id=910") == 1,
            "concurrent tabs or restored backup commit exactly one payment");

        await Reset(); localId = Guid.NewGuid().ToString();
        var partial = await Send(Dto(localId, 50));
        check(!partial.Success && partial.ErrorCode == "PAYMENT_DECISION_REQUIRED" && partial.Decision != null, "offline partial exposes the structured administrative decision");
        check(await Count("payments", "client_id=910") == 0 && await Count("offline_payment_receipts", $"local_id='{localId}'") == 0,
            "decision consultation rolls back both payment and receipt");
        var approved = Dto(localId, 50);
        approved.FutureDebitAction = "keep_month_pending";
        approved.PaymentDecisionToken = partial.Decision!.DecisionToken;
        approved.ExpectedPaymentStateToken = partial.Decision.ExpectedPaymentStateToken;
        var confirmation = await Send(approved);
        check(confirmation.Success && await Count("payments", "client_id=910") == 1, "explicit decision confirms original local payment");
        var replayWithOldToken = Dto(localId, 50);
        replayWithOldToken.ExpectedPaymentStateToken = "old-state";
        check((await Send(replayWithOldToken)).Success, "known receipt is acknowledged before stale state-token validation");

        var creditMovementId = Convert.ToInt32(await db.ExecuteScalarAsync("SELECT TOP 1 movement_id FROM account_movements WHERE payment_id=@paymentId AND movement_type='CREDITO' ORDER BY movement_id",
            [new SqlParameter("@paymentId", confirmation.PaymentId!.Value)]));
        await payments.DeletePaymentAsync(creditMovementId);
        var deletedReplay = await Send(Dto(localId, 50));
        check(!deletedReplay.Success && deletedReplay.ErrorCode == "OFFLINE_PAYMENT_DELETED" && await Count("payments", "client_id=910") == 0,
            "deleting a payment retains its receipt and prevents resurrection from an old backup");

        var invalid = await Send(Dto("not-a-guid"));
        check(!invalid.Success && invalid.ErrorCode == "INVALID_LOCAL_ID", "invalid local identity is rejected before writes");
        var empty = await sync.ProcessOfflinePaymentsAsync(new());
        check(empty.Results.Count == 0, "empty offline batch is harmless");

        var monthlyMigration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "interest-settings-migration.sql"));
        await db.ExecuteCommandAsync(monthlyMigration);
        await db.ExecuteCommandAsync("INSERT monthly_interest_settings(effective_date,percentage,apply_day) VALUES(@month,12.5,15)", [new SqlParameter("@month", month)]);
        var snapshot = await sync.GetSnapshotAsync();
        check(snapshot.SchemaVersion == 1 && snapshot.Clients.Any(c => c.Id == clientId), "snapshot keeps the active-client contract and declares its format");
        check(snapshot.MonthlyInterest!.Settings.Any(s => s.EffectiveDate == month && s.Percentage == 12.5m && s.ApplyDay == 15), "snapshot includes configured interest rules for offline reload");
        var controller = new SyncController(sync, NullLogger<SyncController>.Instance) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        check(await controller.GetStatus(db) is OkObjectResult, "availability probe checks the actual disposable database");
    }
}
