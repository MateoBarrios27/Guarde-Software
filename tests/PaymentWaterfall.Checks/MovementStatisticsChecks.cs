using GuardeSoftwareAPI.Controllers;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.AccountMovement;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.accountMovement;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

internal static class MovementStatisticsChecks
{
    public static async Task RunAsync(AccessDB db, Action<bool, string> check)
    {
        await db.ExecuteCommandAsync("""
            INSERT clients(client_id,full_name,payment_identifier,active) VALUES(1101,'Histórico',11.01,0),(1102,'Otro',11.02,1);
            SET IDENTITY_INSERT rentals ON;
            INSERT rentals(rental_id,client_id,start_date,active,pending_surcharge) VALUES
                (1101,1101,'2026-01-01',0,99999),(1102,1101,'2026-10-01',1,0),(1103,1102,'2026-01-01',1,0);
            SET IDENTITY_INSERT rentals OFF;
            INSERT rental_amount_history(rental_id,amount,start_date,end_date) VALUES
                (1101,232000,'2026-01-01','2026-08-31'),(1101,250000,'2026-09-01',NULL);
            SET IDENTITY_INSERT account_movements ON;
            INSERT account_movements(movement_id,rental_id,movement_date,movement_type,concept,amount) VALUES
                (11001,1101,'2026-08-01','DEBITO','Interés por mora de Julio 2026',23200),
                (11002,1101,'2026-08-01','DEBITO','Alquiler Agosto 2026',232000),
                (11003,1101,'2026-09-01','DEBITO','Interés por mora de Agosto 2026',25500),
                (11004,1101,'2026-09-01','DEBITO','Alquiler Septiembre 2026',232000),
                (11005,1101,'2026-09-14','CREDITO','Pago parcial',170000),
                (11006,1101,'2026-09-15','CREDITO','Segundo pago',100000),
                (11007,1101,'2026-09-16','CREDITO','Pago completo',242700),
                (11008,1101,'2026-09-17','CREDITO','Saldo a favor',10000),
                (11009,1101,'2026-10-01','DEBITO','Alquiler Octubre 2026 (Planificado)',250000),
                (11010,1102,'2026-10-01','DEBITO','Alquiler Octubre 2026',900000),
                (11011,1103,'2026-01-01','CREDITO','Otro cliente',1);
            SET IDENTITY_INSERT account_movements OFF;
            """);
        var service = new AccountMovementService(db, NullLogger<AccountMovementService>.Instance,
            new ClientMonthBalanceService(db), null!, null!);
        var controller = new AccountMovementController(service, NullLogger<AccountMovementController>.Instance);
        var expected = new[]
        {
            (11001,0m,23200m,232000m,-23200m),
            (11002,0m,23200m,232000m,-255200m),
            (11003,-232000m,48700m,250000m,-280700m),
            (11004,-232000m,48700m,250000m,-512700m),
            (11005,-62000m,48700m,250000m,-342700m),
            (11006,0m,10700m,250000m,-242700m),
            (11007,0m,0m,250000m,0m),
            (11008,10000m,0m,250000m,10000m),
            (11009,0m,0m,250000m,-240000m)
        };
        foreach (var (id, prior, interest, rent, balance) in expected)
        {
            var actual = await service.GetMovementStatisticsAsync(1101,id);
            check(actual.MovementId == id && actual.RentalId == 1101, $"movement {id}: correct historical rental");
            check(actual.PreviousBalance == prior && actual.InterestAmount == interest && actual.CurrentRent == rent && actual.Balance == balance,
                $"movement {id}: prior principal, interest, historical rent and balance");
        }
        var current = await service.GetMovementStatisticsAsync(1101,11010);
        check(current.Balance == -900000 && current.CurrentRent == null, "reactivation contract is isolated and missing rent is not invented");
        check(await controller.GetMovementStatistics(1102,11005) is NotFoundObjectResult, "movement from a different client returns 404");
        check(await controller.GetMovementStatistics(1101,999999) is NotFoundObjectResult, "missing movement returns 404");
        check(await controller.GetMovementStatistics(0,11005) is BadRequestObjectResult, "invalid IDs return 400");
        check(await controller.GetMovementStatistics(1101,11005) is OkObjectResult { Value: MovementStatisticsDto }, "authenticated controller action returns statistics");
        // Corrections are replayed from current records, not from stale persisted snapshots.
        await db.ExecuteCommandAsync("UPDATE account_movements SET amount=200000 WHERE movement_id=11005");
        check((await service.GetMovementStatisticsAsync(1101,11005)).PreviousBalance == -32000, "editing an earlier amount updates the reconstruction");
        await db.ExecuteCommandAsync("DELETE account_movements WHERE movement_id=11005");
        check((await service.GetMovementStatisticsAsync(1101,11006)).PreviousBalance == -132000, "deleting an earlier credit updates later reconstruction");
        check(Convert.ToInt32(await db.ExecuteScalarAsync("SELECT COUNT(*) FROM client_month_balances WHERE rental_id IN (1101,1102,1103)")) == 0,
            "reading statistics does not rebuild or write monthly balances");
        check(Convert.ToDecimal(await db.ExecuteScalarAsync("SELECT pending_surcharge FROM rentals WHERE rental_id=1101")) == 99999,
            "reserved surcharge is untouched and not added as a historical debit");
        var sameDay = new[]
        {
            new AccountMovement { Id=1,RentalId=1,MovementDate=new DateTime(2026,8,31,9,0,0),MovementType="DEBITO",Concept="Alquiler Agosto 2026",Amount=100 },
            new AccountMovement { Id=2,RentalId=1,MovementDate=new DateTime(2026,8,31,10,0,0),MovementType="CREDITO",Amount=50 },
            new AccountMovement { Id=3,RentalId=1,MovementDate=new DateTime(2026,8,31,11,0,0),MovementType="CREDITO",Amount=50 }
        };
        check(MovementStatisticsCalculator.Calculate(sameDay,sameDay[1],100).Balance == -50, "same-day later movements are excluded by timestamp");
        check(sameDay[0].Amount == 100 && sameDay[1].Amount == 50, "calculation leaves source ledger unchanged");
        await db.ExecuteCommandAsync("INSERT account_movements(rental_id,movement_date,movement_type,concept,amount) VALUES(1101,'2026-08-31T12:00:00','CREDITO','Boundary',1)");
        var boundary = Convert.ToInt32(await db.ExecuteScalarAsync("SELECT MAX(movement_id) FROM account_movements"));
        check((await service.GetMovementStatisticsAsync(1101,boundary)).CurrentRent == 232000, "rent end date covers the entire final calendar day");
    }
}
