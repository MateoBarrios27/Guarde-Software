using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Services.sync;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ClientSearchChecks
{
    public static async Task RunAsync(AccessDB db, Action<bool, string> check)
    {
        await db.ExecuteCommandAsync("""
            INSERT clients(client_id,full_name,payment_identifier,dni,cuit,active,initial_amount)
            VALUES(901,'Cliente Busqueda Uno',901.12,'30000111','20-30000111-9',1,100),
                  (902,'Cliente Busqueda Dos',902.34,NULL,'20123456787',1,100),
                  (903,'Cliente Busqueda Baja',903.56,NULL,'27-98765432-1',0,100),
                  (904,'Cliente Busqueda Sin Datos',904.78,NULL,NULL,1,100);
            INSERT emails(client_id,address,active) VALUES
                (901,'principal@ejemplo.test',1),(901,'secundario@empresa.test',1),
                (901,'otra@empresa.test',1),(901,'eliminado@viejo.test',0),
                (902,'otro@ejemplo.test',1),(903,'baja@empresa.test',1);
            SET IDENTITY_INSERT rentals ON;
            INSERT rentals(rental_id,client_id,start_date,active,months_unpaid,pending_surcharge)
            VALUES(901,901,'2026-01-01',1,0,0),(902,902,'2026-01-01',1,0,0);
            SET IDENTITY_INSERT rentals OFF;
            INSERT warehouses VALUES(901,'Depósito búsqueda');
            INSERT lockers(locker_id,identifier,rental_id,warehouse_id,locker_type_id,active)
            VALUES(901,'M-77',NULL,901,1,1);
            INSERT rental_lockers VALUES(901,901),(902,901);
            """);
        var dao = new DaoClient(db);
        async Task Search(string term, int[] expected, bool active = true, int pageSize = 100)
        {
            var result = await dao.GetTableClientsAsync(new GetClientsRequestDto
                { SearchTerm=term, Active=active, PageSize=pageSize });
            check(result.totalCount == expected.Length, $"search '{term}' count matches");
            check(result.clients.Select(c=>c.Id).Order().SequenceEqual(expected.Order().Take(pageSize)),
                $"search '{term}' returns expected unique clients");
        }
        await Search("20-30000111-9", [901]);
        await Search("20300001119", [901]);
        await Search("20.30000111.9", [901]);
        await Search(" 20 30000111 9 ", [901]);
        await Search("20-12345678-7", [902]);
        await Search("30000111", [901]);
        await Search("secundario@empresa.test", [901]);
        await Search("SECUNDARIO@EMPRESA.TEST", [901]);
        await Search("@empresa.test", [901]);
        await Search("@empresa.test", [901], pageSize:1);
        await Search("eliminado@viejo.test", []);
        await Search("27123456789", []);
        await Search("27987654321", [903], active:false);
        await Search("baja@empresa.test", [903], active:false);
        await Search("M-77", [901,902]);
        await Search("Cliente Busqueda Uno", [901]);
        await Search("901.12", [901]);
        var snapshot = await new SyncService(db, null!, NullLogger<SyncService>.Instance).GetSnapshotAsync();
        var cached = snapshot.Clients.Single(c=>c.Id==901);
        check(cached.Cuit=="20-30000111-9" && cached.Dni=="30000111", "snapshot contains CUIT and DNI");
        check(cached.Emails.SequenceEqual(new[]{"principal@ejemplo.test","secundario@empresa.test","otra@empresa.test"}),
            "snapshot contains all active emails in display order");
        check(snapshot.Clients.Single(c=>c.Id==904).Emails.Count==0,
            "snapshot tolerates a client without email or CUIT");
    }
}
