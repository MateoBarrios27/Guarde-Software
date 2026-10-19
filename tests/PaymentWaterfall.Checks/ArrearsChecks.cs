using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Services.sync;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ArrearsChecks
{
    public static async Task RunAsync(AccessDB db, ClientMonthBalanceService balances, Action<bool, string> check)
    {
        await db.ExecuteCommandAsync("CREATE TABLE users(user_id INT PRIMARY KEY); INSERT users VALUES(1);");
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "notifications-migration.sql"));
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(migration, @"(?im)^GO\s*$"))
            if (!string.IsNullOrWhiteSpace(batch)) await db.ExecuteCommandAsync(batch);
        var dismissalMigration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "notification-dismissal-migration.sql"));
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(dismissalMigration, @"(?im)^GO\s*$"))
            if (!string.IsNullOrWhiteSpace(batch)) await db.ExecuteCommandAsync(batch);
        await db.ExecuteCommandAsync("""
            DELETE account_movements; DELETE client_month_balances;
            INSERT account_movements(rental_id,movement_date,movement_type,concept,amount) VALUES
            (1,'2026-07-01','DEBITO','Alquiler Julio 2026',100),
            (1,'2026-08-01','DEBITO','Alquiler Agosto 2026',100),
            (1,'2026-09-01','DEBITO','Alquiler Septiembre 2026',100),
            (1,'2026-10-01','DEBITO','Alquiler Octubre 2026',100),
            (1,'2026-11-01','DEBITO','Alquiler Noviembre 2026',100);
            """);
        await balances.RebuildForRentalAsync(1);
        async Task<int> Count(string date) => Convert.ToInt32(await db.ExecuteScalarAsync(
            $"SELECT dbo.GetRentalUnpaidMonths(1, CONVERT(date, '{date}', 23));"));
        check(await Count("2026-07-31") == 0, "July is not closed during July 31");
        check(await Count("2026-08-01") == 1, "first unpaid month closes on August 1");
        check(await Count("2026-09-01") == 2, "two unpaid months on September 1");
        check(await Count("2026-09-30") == 2, "third month is not closed during September 30");
        check(await Count("2026-10-01") == 3, "third unpaid month closes on October 1; future rent excluded");
        check(await Count("2027-01-01") == 6, "elapsed closed months remain available above level three across years");
        var currentCount = await Count(DateTime.UtcNow.AddHours(-3).ToString("yyyy-MM-dd"));
        var clients = await new DaoClient(db).GetTableClientsAsync(new GetClientsRequestDto
            { Active = true, PageSize = 100, StatusFilter = "Moroso" });
        check(clients.totalCount == (currentCount > 0 ? 1 : 0), "generic overdue filter includes numbered levels");
        if (currentCount > 0)
            check(clients.clients.Single().Status == "Moroso Nivel " + Math.Min(currentCount, 3), "client table shows capped level from closed months");
        var snapshot = await new SyncService(db, null!, NullLogger<SyncService>.Instance).GetSnapshotAsync();
        check(snapshot.Clients.Single().MonthsUnpaid == currentCount
            && (currentCount == 0 || snapshot.Clients.Single().Status == clients.clients.Single().Status),
            "offline snapshot and table agree on months and level");
        var statistics = await new DaoStatistics(db).GetClientStatisticsAsync();
        check(statistics.Morosos == (currentCount > 0 ? 1 : 0), "statistics agree with overdue client table");
        var notifications = new DaoNotification(db);
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 9, 30)) == 0, "no level three notification before third closure");
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 1)) == 1, "level three creates one persistent notification");
        var inbox = await notifications.GetInboxAsync(1, 50);
        long id = Convert.ToInt64(inbox.Rows[0]["notification_id"]);
        check(inbox.Rows.Count == 1 && inbox.Rows[0]["severity"].ToString() == "danger"
            && inbox.Rows[0]["action_url"].ToString() == "/clients?detailClientId=1", "critical notification links to client");
        await notifications.MarkAsReadAsync(id, 1);
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 2)) == 0
            && await notifications.GetUnreadCountAsync(1) == 0, "repeat synchronization preserves read state and does not duplicate");
        await db.ExecuteCommandAsync("""
            INSERT account_movements(rental_id,movement_date,movement_type,concept,amount)
            VALUES(1,'2026-10-02','CREDITO','Pago parcial',50);
            """);
        await balances.RebuildForRentalAsync(1);
        check(await Count("2026-10-03") == 2, "without manual decision, historical partial coverage advances the collection month");
        await db.ExecuteCommandAsync("""
            INSERT payments(client_id,payment_date,amount,payment_method_id) VALUES(1,'2026-10-02',50,1);
            INSERT payment_collection_decisions(payment_id,rental_id,next_payment_month,action,ledger_anchor_id)
            SELECT MAX(payment_id),1,'2026-07-01','keep_month_pending',
                (SELECT MAX(movement_id) FROM account_movements WHERE rental_id=1) FROM payments;
            """);
        check(await Count("2026-10-03") == 3, "keep partially paid July pending preserves three months of arrears");
        await db.ExecuteCommandAsync("UPDATE payment_collection_decisions SET next_payment_month='2026-08-01', action='close_partial_month';");
        check(await Count("2026-10-03") == 2, "contemplating July as paid lowers arrears despite outstanding July principal");
        check(Convert.ToDecimal(await db.ExecuteScalarAsync("SELECT unpaid_rent FROM client_month_balances WHERE rental_id=1 AND month_year='07/2026'")) == 50,
            "collection decision does not forgive remaining principal");
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 3)) == 1,
            "advancing chosen collection month resolves level three notification");
        await db.ExecuteCommandAsync("UPDATE payment_collection_decisions SET next_payment_month='2026-07-01', action='keep_month_pending';");
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 3)) == 1,
            "keeping same partial month pending reopens critical notification");
        await db.ExecuteCommandAsync("""
            INSERT account_movements(rental_id,movement_date,movement_type,concept,amount)
            VALUES(1,'2026-10-03','CREDITO','Completa julio',50);
            """);
        await balances.RebuildForRentalAsync(1);
        check(await Count("2026-10-04") == 2, "payment reduces months according to allocation, despite future debt");
        check(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentCollectionMonth(1)") == DBNull.Value,
            "later manual credit invalidates the previous collection decision");
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 4)) == 1
            && (await notifications.GetInboxAsync(1, 50)).Rows.Count == 0, "dropping below three resolves notification");
        await db.ExecuteCommandAsync("DELETE account_movements WHERE concept IN ('Pago parcial','Completa julio');");
        await db.ExecuteCommandAsync("DELETE payments;");
        await balances.RebuildForRentalAsync(1);
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 4)) == 1
            && await notifications.GetUnreadCountAsync(1) == 1, "deleting payments restores arrears and reopens unread notification");
        await db.ExecuteCommandAsync("UPDATE clients SET active=0 WHERE client_id=1;");
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 4)) == 1, "inactive client notification resolves");
        await db.ExecuteCommandAsync("UPDATE clients SET active=1 WHERE client_id=1; UPDATE rentals SET active=0 WHERE rental_id=1;");
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 4)) == 0, "inactive rental does not notify");
        await db.ExecuteCommandAsync("""
            UPDATE rentals SET active=1 WHERE rental_id=1;
            INSERT account_movements(rental_id,movement_date,movement_type,concept,amount)
            VALUES(1,'2026-10-01','DEBITO','Interés por mora',200),
                  (1,'2026-10-05','CREDITO','Cancela capital anterior',300);
            """);
        await balances.RebuildForRentalAsync(1);
        check(await Count("2026-10-06") == 0, "only unpaid interest and current/future rent do not create arrears levels");
        // A second rental of the same client must not create a second notification.
        await db.ExecuteCommandAsync("""
            DELETE account_movements WHERE concept='Cancela capital anterior';
            INSERT rentals(client_id,start_date,active,months_unpaid) VALUES(1,'2026-01-01',1,99);
            """);
        await balances.RebuildForRentalAsync(1);
        check(await notifications.SyncArrearsNotificationsAsync(new(2026, 10, 6)) == 1
            && (await notifications.GetInboxAsync(1, 50)).Rows.Count == 1, "notification is unique per client and ignores stale counters");
        var rentalDao = new DaoRental(db);
        await rentalDao.SavePendingSurchargeAsync(1, 10, 100, new(2026, 10, 11));
        await rentalDao.SavePendingSurchargeAsync(1, 10, 100, new(2026, 10, 11));
        check(await Count("2026-10-11") == 3, "day eleven surcharge does not advance arrears level");
        await db.ExecuteCommandAsync("""
            INSERT clients(client_id,full_name,active,is_deleted,initial_amount) VALUES
                (901,'Filtro mora uno',1,0,100),(902,'Filtro mora dos',1,0,100),
                (903,'Filtro mora cuatro',1,0,100),(904,'Filtro mora actual',1,0,100);
            SET IDENTITY_INSERT rentals ON;
            INSERT rentals(rental_id,client_id,start_date,active,months_unpaid) VALUES
                (901,901,'2020-01-01',1,0),(902,902,'2020-01-01',1,0),
                (903,903,'2020-01-01',1,0),(904,904,'2020-01-01',1,0);
            SET IDENTITY_INSERT rentals OFF;
            DECLARE @Month DATE = DATEFROMPARTS(YEAR(DATEADD(hour,-3,GETUTCDATE())),MONTH(DATEADD(hour,-3,GETUTCDATE())),1);
            INSERT client_month_balances(rental_id,month_year,monthly_debits,balance,paid,advanced_payment,allocated_rent,allocated_interests)
            SELECT id, FORMAT(DATEADD(month,-age,@Month),'MM/yyyy'),100,100,0,0,0,0
            FROM (VALUES (901,1),(902,2),(903,4),(904,0)) fixture(id,age);
            """);
        async Task Filter(int[]? included, int[]? excluded, int[] expected, int pageSize = 100)
        {
            var result = await new DaoClient(db).GetTableClientsAsync(new GetClientsRequestDto
            {
                SearchTerm = "Filtro mora", Active = true, PageSize = pageSize,
                ArrearsLevels = included?.ToList(), ExcludedArrearsLevels = excluded?.ToList(),
                SortField = "FullName"
            });
            check(result.totalCount == expected.Length, "arrears filter count includes all matches before pagination");
            if (pageSize >= expected.Length)
                check(result.clients.Select(c => c.Id).Order().SequenceEqual(expected.Order()),
                    "arrears include/exclude returns expected client levels");
            else check(result.clients.Count == pageSize, "arrears filter respects page size");
        }
        await Filter([1], null, [901]);
        await Filter([2], null, [902]);
        await Filter([3], null, [903]);
        await Filter([1, 2], null, [901, 902]);
        await Filter([1, 2], [2], [901]);
        await Filter(null, [3], [901, 902, 904]);
        await Filter([1, 1, 2], null, [901, 902], 1);
        await Filter([0, 4], null, [901, 902, 903, 904]);
    }
}
