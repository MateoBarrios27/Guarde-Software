using GuardeSoftwareAPI.Dao;

internal static class ClientStatisticsChecks
{
    public static async Task RunAsync(AccessDB db, Action<bool, string> check)
    {
        // Only the disposable test DB created by Program. Dates follow the Argentine month.
        await db.ExecuteCommandAsync("""
            UPDATE clients SET is_deleted = 1;
            INSERT clients(client_id,full_name,active,is_deleted) VALUES
                (201,'Nivel 1',1,0),(202,'Nivel 2',1,0),(203,'Nivel 3 o mayor',1,0),
                (204,'Plan futuro sin pago',1,0),(205,'Mes cubierto',1,0),
                (206,'Parcial mantenido',1,0),(207,'Parcial contemplado',1,0),
                (208,'Parcial anterior mantenido',1,0),(209,'Baja',0,0),
                (210,'Eliminado',1,1),(211,'Dos alquileres',1,0),(212,'Sin alquiler',1,0);
            SET IDENTITY_INSERT rentals ON;
            INSERT rentals(rental_id,client_id,start_date,active,months_unpaid) VALUES
                (201,201,'2020-01-01',1,0),(202,202,'2020-01-01',1,0),(203,203,'2020-01-01',1,0),
                (204,204,'2020-01-01',1,0),(205,205,'2020-01-01',1,99),
                (206,206,'2020-01-01',1,0),(207,207,'2020-01-01',1,0),
                (208,208,'2020-01-01',1,0),(209,209,'2020-01-01',0,0),
                (210,210,'2020-01-01',1,0),(211,211,'2020-01-01',1,0),
                (213,211,'2026-01-01',1,0);
            SET IDENTITY_INSERT rentals OFF;
            DECLARE @Month DATE = DATEFROMPARTS(YEAR(DATEADD(hour,-3,GETUTCDATE())),MONTH(DATEADD(hour,-3,GETUTCDATE())),1);
            INSERT client_month_balances(rental_id,month_year,monthly_debits,balance,paid,advanced_payment,allocated_rent,allocated_interests)
            SELECT id,FORMAT(DATEADD(month,month_offset,@Month),'MM/yyyy'),100,100,covered,0,covered,0
            FROM (VALUES (201,-1,0),(202,-2,0),(203,-4,0),
                (204,0,0),(204,1,0),(204,2,0),
                (205,0,100),(205,1,0),
                (206,0,50),(206,1,0),(207,0,50),(207,1,0),
                (208,-1,50),(208,0,0),(209,-1,0),(210,-2,0),
                (211,-2,0),(213,0,100),(213,1,0)) fixture(id,month_offset,covered);
            INSERT payments(client_id,payment_date,amount) VALUES (206,@Month,50),(207,@Month,50),(208,@Month,50);
            INSERT payment_collection_decisions(payment_id,rental_id,next_payment_month,action,ledger_anchor_id)
            SELECT payment_id,client_id,
                CASE client_id WHEN 207 THEN DATEADD(month,1,@Month) WHEN 208 THEN DATEADD(month,-1,@Month) ELSE @Month END,
                CASE client_id WHEN 207 THEN 'close_partial_month' ELSE 'keep_month_pending' END,0
            FROM payments WHERE client_id IN (206,207,208);
            """);
        var dao = new DaoStatistics(db);
        async Task Verify(int paid, int overdue, int pending, string context)
        {
            var stats = await dao.GetClientStatisticsAsync();
            check(stats.Total == 10 && stats.DadosBaja == 1, context + ": global active total and inactive total exclude deleted clients");
            check(stats.Morosos == overdue, context + ": levels 1, 2 and 3 have priority; every active rental is considered");
            check(stats.AlDia == paid && stats.Pendientes == pending, context + ": collection month determines paid and unpaid outside arrears");
            check(stats.Total == stats.Morosos + stats.AlDia + stats.Pendientes, context + ": three disjoint groups partition all active clients");
        }
        await Verify(2, 5, 3, "initial classification");
        await db.ExecuteCommandAsync("""
            UPDATE payment_collection_decisions SET next_payment_month=DATEADD(month,1,next_payment_month),action='close_partial_month'
            WHERE rental_id=206;
            """);
        await Verify(3, 5, 2, "contemplating the current partial month");
        await db.ExecuteCommandAsync("""
            UPDATE payment_collection_decisions SET next_payment_month=DATEADD(month,-1,next_payment_month),action='keep_month_pending'
            WHERE rental_id=207;
            """);
        await Verify(2, 5, 3, "keeping the current partial month pending");
        await db.ExecuteCommandAsync("""
            UPDATE payment_collection_decisions SET next_payment_month=DATEADD(month,1,next_payment_month),action='close_partial_month'
            WHERE rental_id=208;
            """);
        await Verify(2, 4, 4, "contemplating the old partial month removes its arrears level");
        await db.ExecuteCommandAsync("UPDATE clients SET is_deleted=1;");
        var empty = await dao.GetClientStatisticsAsync();
        check(empty.Total == 0 && empty.AlDia == 0 && empty.Morosos == 0 && empty.Pendientes == 0 && empty.DadosBaja == 0,
            "empty statistics contain zero counts");
    }
}
