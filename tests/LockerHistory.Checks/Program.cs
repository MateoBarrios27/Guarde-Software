using System.Text.RegularExpressions;
using GuardeSoftwareAPI.Dao;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Disposable database only; the application connection is never read.
var name = "GuardeLockerHistoryChecks_" + Guid.NewGuid().ToString("N");
var cs = new SqlConnectionStringBuilder { DataSource = args.FirstOrDefault() ?? @".\SQLEXPRESS", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
using var master = new SqlConnection(cs.ConnectionString);
await master.OpenAsync();
using var admin = master.CreateCommand();
admin.CommandText = $"CREATE DATABASE [{name}]";
await admin.ExecuteNonQueryAsync();
try
{
    cs.InitialCatalog = name;
    var db = new AccessDB(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = cs.ConnectionString }).Build());
    var dao = new DaoClient(db);
    await db.ExecuteCommandAsync(@"CREATE TABLE client_locker_history (
        history_id INT IDENTITY PRIMARY KEY, client_id INT NOT NULL, locker_id INT NOT NULL,
        start_date DATETIME NOT NULL, end_date DATETIME NULL, notes NVARCHAR(255) NULL);");
    var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migration.sql"));
    async Task Migrate() { foreach (var batch in Regex.Split(script, @"^GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)) if (!string.IsNullOrWhiteSpace(batch)) await db.ExecuteCommandAsync(batch); }
    await Migrate();
    async Task Reset() => await db.ExecuteCommandAsync("DELETE client_locker_history; DELETE client_locker_history_merge_archive;");
    async Task Add(int client, int locker, DateTime start, DateTime? end, string? notes = null) => await db.ExecuteCommandAsync(
        "INSERT client_locker_history VALUES(@client, @locker, @start, @end, @notes)",
        [new("@client", client), new("@locker", locker), new("@start", start), new("@end", (object?)end ?? DBNull.Value), new("@notes", (object?)notes ?? DBNull.Value)]);
    async Task<int> Number(string sql) => Convert.ToInt32(await db.ExecuteScalarAsync(sql));
    void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
    var start = new DateTime(2026, 1, 1, 12, 0, 0);
    var end = start.AddDays(10);
    foreach (var gap in new[] { TimeSpan.Zero, TimeSpan.FromDays(29), TimeSpan.FromDays(30), TimeSpan.FromDays(30).Add(TimeSpan.FromSeconds(1)), TimeSpan.FromDays(31) })
    {
        await Reset(); await Add(1, 1, start, end, "Anterior"); await Add(1, 1, end + gap, null, "Actual");
        var oldId = await Number("SELECT MIN(history_id) FROM client_locker_history");
        await db.ExecuteCommandAsync("EXEC dbo.MergeClientLockerHistory30Days");
        bool merge = gap <= TimeSpan.FromDays(30);
        Check(await Number("SELECT COUNT(*) FROM client_locker_history") == (merge ? 1 : 2), "gap " + gap);
        if (merge)
        {
            Check(await Number("SELECT history_id FROM client_locker_history") == oldId, "original row reused");
            Check(await Number("SELECT COUNT(*) FROM client_locker_history WHERE end_date IS NULL AND start_date='2026-01-01T12:00:00'") == 1, "old start and active status preserved");
        }
    }
    await Reset();
    await Add(1, 1, start, end, new string('a', 200));
    await Add(1, 1, end.AddDays(20), end.AddDays(25), new string('b', 200));
    await Add(1, 1, end.AddDays(45), null, "Activo");
    await Add(2, 1, start, null, "Otro cliente");
    await Add(1, 2, start, null, "Otra baulera");
    await db.ExecuteCommandAsync("EXEC dbo.MergeClientLockerHistory30Days @ClientId=1, @LockerId=1");
    Check(await Number("SELECT COUNT(*) FROM client_locker_history WHERE client_id=1 AND locker_id=1") == 1, "three chained rows become one");
    Check(await Number("SELECT COUNT(*) FROM client_locker_history") == 3, "other clients and lockers untouched");
    Check(await Number("SELECT LEN(notes) FROM client_locker_history WHERE client_id=1 AND locker_id=1") > 400, "notes not truncated");
    Check(await Number("SELECT COUNT(*) FROM client_locker_history_merge_archive") == 3, "original rows archived");
    await Migrate();
    Check(await Number("SELECT COUNT(*) FROM client_locker_history_merge_archive") == 3, "migration idempotent");
    await Reset();
    await Add(1, 1, start, end); await Add(1, 1, end.AddDays(2), end.AddDays(5));
    await db.ExecuteCommandAsync("EXEC dbo.MergeClientLockerHistory30Days");
    Check(await Number("SELECT COUNT(*) FROM client_locker_history WHERE end_date='2026-01-16T12:00:00'") == 1, "closed group keeps latest end");
    await Reset();
    await Add(1, 1, start, DateTime.UtcNow.AddHours(-3).AddDays(-5));
    var originalId = await Number("SELECT history_id FROM client_locker_history");
    using (var connection = db.GetConnectionClose())
    {
        await connection.OpenAsync(); using var tx = connection.BeginTransaction();
        await dao.OpenLockerHistoryTransactionAsync(1, [1, 1], connection, tx);
        await dao.OpenLockerHistoryTransactionAsync(1, [1], connection, tx);
        await tx.CommitAsync();
    }
    Check(await Number("SELECT COUNT(*) FROM client_locker_history") == 1 && await Number("SELECT history_id FROM client_locker_history") == originalId, "DAO reopening and duplicate calls keep original row");
    using (var connection = db.GetConnectionClose())
    {
        await connection.OpenAsync(); using var tx = connection.BeginTransaction();
        await dao.CloseLockerHistoryTransactionAsync(1, [1], connection, tx);
        await dao.OpenLockerHistoryTransactionAsync(1, [1], connection, tx);
        await tx.RollbackAsync();
    }
    Check(await Number("SELECT COUNT(*) FROM client_locker_history WHERE end_date IS NULL") == 1, "caller transaction controls rollback");
    Console.WriteLine("All locker history checks passed.");
}
finally
{
    SqlConnection.ClearAllPools();
    // Name is generated locally, never supplied by a caller.
    admin.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
    await admin.ExecuteNonQueryAsync();
}
