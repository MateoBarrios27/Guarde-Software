using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Disposable database only; the application database is never read or changed.
var name = "GuardeLockerAssignmentChecks_" + Guid.NewGuid().ToString("N");
var cs = new SqlConnectionStringBuilder
{
    DataSource = args.FirstOrDefault() ?? @".\SQLEXPRESS",
    InitialCatalog = "master",
    IntegratedSecurity = true,
    TrustServerCertificate = true,
    ConnectTimeout = 5
};

using var master = new SqlConnection(cs.ConnectionString);
await master.OpenAsync();
using var admin = master.CreateCommand();
admin.CommandText = $"CREATE DATABASE [{name}]";
await admin.ExecuteNonQueryAsync();

try
{
    cs.InitialCatalog = name;
    var db = new AccessDB(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = cs.ConnectionString })
        .Build());
    var dao = new DaoLocker(db);

    await db.ExecuteCommandAsync(@"
        CREATE TABLE clients (
            client_id INT PRIMARY KEY,
            full_name VARCHAR(200) NOT NULL,
            payment_identifier INT NULL
        );
        CREATE TABLE rentals (
            rental_id INT PRIMARY KEY,
            client_id INT NOT NULL,
            active BIT NOT NULL
        );
        CREATE TABLE lockers (
            locker_id INT PRIMARY KEY,
            warehouse_id INT NOT NULL,
            locker_type_id INT NOT NULL,
            identifier VARCHAR(100) NULL,
            features VARCHAR(500) NULL,
            status VARCHAR(50) NOT NULL,
            rental_id INT NULL,
            is_free_space BIT NOT NULL,
            active BIT NOT NULL
        );
        CREATE TABLE rental_lockers (
            rental_id INT NOT NULL,
            locker_id INT NOT NULL,
            CONSTRAINT PK_rental_lockers PRIMARY KEY (rental_id, locker_id)
        );
        INSERT clients VALUES (1, 'Cliente Uno', 101), (2, 'Cliente Dos', 202);
        INSERT rentals VALUES (10, 1, 1), (20, 2, 1);
        INSERT lockers VALUES
            (1, 1, 1, 'A-1', '', 'OCUPADO', 10, 0, 1),
            (2, 1, 1, 'A-2', '', 'OCUPADO', 20, 1, 1),
            (3, 1, 1, 'A-3', '', 'OCUPADO', NULL, 1, 1);
        INSERT rental_lockers VALUES (10, 3), (20, 3);");

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }

    async Task<int> Number(string sql) => Convert.ToInt32(await db.ExecuteScalarAsync(sql));
    async Task Update(Locker locker)
    {
        using var connection = db.GetConnectionClose();
        await connection.OpenAsync();
        using var tx = connection.BeginTransaction();
        await dao.UpdateLockerPreservingAssignmentsTransactionAsync(locker, connection, tx);
        await tx.CommitAsync();
    }

    await Update(new Locker
    {
        Id = 1,
        WarehouseId = 1,
        LockerTypeId = 1,
        Identifier = "A-1",
        Features = "",
        Status = "OCUPADO",
        IsFreeSpace = true
    });
    Check(await Number("SELECT COUNT(*) FROM lockers WHERE locker_id=1 AND is_free_space=1 AND rental_id IS NULL") == 1,
        "normal to multi clears legacy rental_id");
    Check(await Number("SELECT COUNT(*) FROM rental_lockers WHERE locker_id=1 AND rental_id=10") == 1,
        "normal to multi preserves assigned client");

    var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migration.sql"));
    await db.ExecuteCommandAsync(migration);
    await db.ExecuteCommandAsync(migration);
    Check(await Number("SELECT COUNT(*) FROM lockers WHERE locker_id=2 AND rental_id IS NULL") == 1,
        "migration clears stale rental_id");
    Check(await Number("SELECT COUNT(*) FROM rental_lockers WHERE locker_id=2 AND rental_id=20") == 1,
        "migration repairs existing multi-client assignment idempotently");

    await Update(new Locker
    {
        Id = 2,
        WarehouseId = 1,
        LockerTypeId = 1,
        Identifier = "A-2",
        Features = "",
        Status = "OCUPADO",
        IsFreeSpace = false
    });
    Check(await Number("SELECT COUNT(*) FROM lockers WHERE locker_id=2 AND is_free_space=0 AND rental_id=20") == 1,
        "single multi assignment returns to normal locker");
    Check(await Number("SELECT COUNT(*) FROM rental_lockers WHERE locker_id=2") == 0,
        "reverse conversion removes join row after preserving assignment");

    bool blockedMultipleClients = false;
    using (var connection = db.GetConnectionClose())
    {
        await connection.OpenAsync();
        using var tx = connection.BeginTransaction();
        try
        {
            await dao.UpdateLockerPreservingAssignmentsTransactionAsync(new Locker
            {
                Id = 3,
                WarehouseId = 1,
                LockerTypeId = 1,
                Identifier = "A-3",
                Features = "",
                Status = "OCUPADO",
                IsFreeSpace = false
            }, connection, tx);
        }
        catch (SqlException ex) when (ex.Number == 51020)
        {
            blockedMultipleClients = true;
        }
        await tx.RollbackAsync();
    }
    Check(blockedMultipleClients, "multi-client locker cannot become normal while two clients are assigned");
    Check(await Number("SELECT COUNT(*) FROM rental_lockers WHERE locker_id=3") == 2,
        "blocked conversion leaves both assignments intact");

    var table = await dao.GetLockers();
    var locker1 = table.Rows.Cast<DataRow>().Single(row => row.Field<int>("locker_id") == 1);
    Check((locker1["clients_json"]?.ToString() ?? "").Contains("Cliente Uno", StringComparison.Ordinal),
        "locker list exposes structured assigned clients");

    Console.WriteLine("All locker assignment checks passed.");
}
finally
{
    SqlConnection.ClearAllPools();
    // Name is generated locally and cannot target an existing database.
    admin.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
    await admin.ExecuteNonQueryAsync();
}
