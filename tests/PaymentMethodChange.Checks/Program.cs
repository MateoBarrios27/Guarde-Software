using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Services.client;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

// Only creates and removes its own uniquely named test database. Never reads appsettings.
var database = "GuardePaymentMethodChecks_" + Guid.NewGuid().ToString("N");
var builder = new SqlConnectionStringBuilder { DataSource = args.FirstOrDefault() ?? @".\SQLEXPRESS", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
using var master = new SqlConnection(builder.ConnectionString);
await master.OpenAsync();
using var masterCommand = master.CreateCommand();
masterCommand.CommandText = $"CREATE DATABASE [{database}]";
await masterCommand.ExecuteNonQueryAsync();
try
{
    builder.InitialCatalog = database;
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = builder.ConnectionString }).Build();
    var db = new AccessDB(config);
    var balances = new ClientMonthBalanceService(db);
    var service = new ClientService(db, NullLogger<ClientService>.Instance, null!, null!, new RentalAmountHistoryService(db, null!), null!, null!, null!, null!, null!, balances);
    await db.ExecuteCommandAsync(@"
        CREATE TABLE clients(client_id INT PRIMARY KEY, preferred_payment_method_id INT);
        CREATE TABLE payment_methods(payment_method_id INT PRIMARY KEY, name NVARCHAR(100), commission DECIMAL(10,2), active BIT);
        CREATE TABLE rentals(rental_id INT PRIMARY KEY, client_id INT, active BIT, increase_anchor_date DATE);
        CREATE TABLE rental_amount_history(rental_amount_history_id INT IDENTITY PRIMARY KEY, rental_id INT, amount DECIMAL(10,2), start_date DATE, end_date DATE);
        CREATE TABLE account_movements(movement_id INT IDENTITY PRIMARY KEY, rental_id INT, movement_date DATETIME, movement_type VARCHAR(10), concept VARCHAR(255), amount DECIMAL(10,2), payment_id INT NULL);
        CREATE TABLE client_month_balances(id INT IDENTITY PRIMARY KEY, rental_id INT, month_year VARCHAR(7), previous_balance DECIMAL(18,2), interests DECIMAL(18,2), monthly_debits DECIMAL(18,2), balance DECIMAL(18,2), paid DECIMAL(18,2), advanced_payment DECIMAL(18,2));
        INSERT payment_methods VALUES(1, 'Efectivo', 0, 1), (2, 'Tarjeta', 10, 1), (3, 'Inactivo', 20, 0);");
    var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migration.sql"));
    await db.ExecuteCommandAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "payment-allocations.sql")));
    await db.ExecuteCommandAsync(migration);
    await db.ExecuteCommandAsync(migration); // idempotence
    async Task Seed(decimal paid = 0, bool advance = false, decimal previousDebt = 0, bool duplicate = false, bool noDebit = false)
    {
        await db.ExecuteCommandAsync(@"
            DELETE client_payment_method_changes; DELETE client_month_balances; DELETE account_movements;
            DELETE rental_amount_history; DELETE rentals; DELETE clients;
            INSERT clients VALUES(1, 1); INSERT rentals VALUES(1, 1, 1, '2099-01-01');
            INSERT rental_amount_history VALUES(1, 100, '2020-01-01', NULL);
            INSERT rental_amount_history VALUES(1, 150, '2099-01-01', NULL);");
        if (!noDebit)
            await db.ExecuteCommandAsync("INSERT account_movements VALUES(1, '2026-09-02', 'DEBITO', 'Alquiler Septiembre 2026', 100, NULL)");
        if (paid > 0)
            await db.ExecuteCommandAsync("INSERT account_movements VALUES(1, @date, 'CREDITO', 'Pago', @paid, 1)", [new SqlParameter("@date", advance ? new DateTime(2026, 8, 1) : new DateTime(2026, 9, 2)), new SqlParameter("@paid", paid)]);
        if (previousDebt > 0)
            await db.ExecuteCommandAsync("INSERT account_movements VALUES(1, '2026-08-01', 'DEBITO', 'Alquiler Agosto 2026', @amount, NULL)", [new SqlParameter("@amount", previousDebt)]);
        if (duplicate)
            await db.ExecuteCommandAsync("INSERT account_movements VALUES(1, '2026-09-01', 'DEBITO', 'Alquiler Septiembre 2026', 100, NULL)");
    }
    async Task<decimal> Scalar(string sql) => Convert.ToDecimal(await db.ExecuteScalarAsync(sql));
    void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    ChangePaymentMethodDto Request() => new() { PaymentMethodId = 2, ExpectedPaymentMethodId = 1, ExpectedAmount = 100, Amount = 110 };

    foreach (var paid in new[] { 0m, 59.99m, 60m, 80m, 100m })
    {
        await Seed(paid);
        await service.ChangePaymentMethodAsync(1, Request());
        Check(await Scalar("SELECT amount FROM account_movements WHERE concept = 'Alquiler Septiembre 2026'") == (paid < 60 ? 110 : 100), $"threshold {paid}");
        Check(await Scalar("SELECT amount FROM rental_amount_history WHERE start_date='2099-01-01'") == 150, "planned increase unchanged");
        var history = await service.GetClientRentalAmountHistoryAsync(1);
        Check(history[0].Status == "active" && history[0].Amount == 110 && history.Count(x => x.Status == "active") == 1, "single active rent first");
        Check(history.Any(x => x.Status == "event" && x.OldPaymentMethod == "Efectivo" && x.NewPaymentMethod == "Tarjeta"), "dated event");
    }
    await Seed(60, advance: true);
    await service.ChangePaymentMethodAsync(1, Request());
    Check(await Scalar("SELECT amount FROM account_movements WHERE movement_type='DEBITO'") == 100, "advance at 60 percent protected");
    await Seed(80, previousDebt: 50);
    await service.ChangePaymentMethodAsync(1, Request());
    Check(await Scalar("SELECT amount FROM account_movements WHERE concept='Alquiler Septiembre 2026'") == 110, "previous debt excluded from coverage");
    Check(await Scalar("SELECT amount FROM account_movements WHERE concept='Alquiler Agosto 2026'") == 50, "older debit unchanged");
    await service.ChangePaymentMethodAsync(1, new() { PaymentMethodId=1, ExpectedPaymentMethodId=2, ExpectedAmount=110, Amount=90 });
    Check(await Scalar("SELECT COUNT(*) FROM client_payment_method_changes") == 2, "two same-day changes retained");
    Check(await Scalar("SELECT SUM(amount) FROM account_movements WHERE movement_type='CREDITO'") == 80, "payments unchanged");
    await Seed(60);
    await db.ExecuteCommandAsync("INSERT account_movements VALUES(1, '2026-09-01', 'DEBITO', 'Interés por mora', 20, NULL)");
    await service.ChangePaymentMethodAsync(1, Request());
    Check(await Scalar("SELECT amount FROM account_movements WHERE concept='Alquiler Septiembre 2026'") == 110, "interest payments excluded from rent coverage");
    Check(await Scalar("SELECT amount FROM account_movements WHERE concept='Interés por mora'") == 20, "interest debit unchanged");
    await Seed();
    await db.ExecuteCommandAsync("ALTER TABLE client_payment_method_changes ADD CONSTRAINT TestFailure CHECK(new_amount <= 100)");
    bool failedAtInsert = false;
    try { await service.ChangePaymentMethodAsync(1, Request()); }
    catch (SqlException) { failedAtInsert = true; }
    Check(failedAtInsert && await Scalar("SELECT amount FROM account_movements") == 100
        && await Scalar("SELECT preferred_payment_method_id FROM clients") == 1
        && (await service.GetPaymentMethodChangeContextAsync(1)).Amount == 100,
        "rollback after updating debit and history when event persistence fails");
    await db.ExecuteCommandAsync("ALTER TABLE client_payment_method_changes DROP CONSTRAINT TestFailure");
    await Seed(noDebit: true);
    await service.ChangePaymentMethodAsync(1, Request());
    Check(await Scalar("SELECT COUNT(*) FROM account_movements") == 0, "does not generate a missing debit");
    foreach (var kind in new[] { "stale", "inactive", "duplicate", "negative", "same method" })
    {
        await Seed(duplicate: kind == "duplicate");
        var request = Request();
        if (kind == "stale") request.ExpectedAmount = 99;
        if (kind == "inactive") request.PaymentMethodId = 3;
        if (kind == "negative") request.Amount = -1;
        if (kind == "same method") request.PaymentMethodId = 1;
        bool rejected = false;
        try { await service.ChangePaymentMethodAsync(1, request); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { rejected = true; }
        Check(rejected && await Scalar("SELECT preferred_payment_method_id FROM clients") == 1 && await Scalar("SELECT COUNT(*) FROM client_payment_method_changes") == 0, "atomic rejection: " + kind);
    }
    Console.WriteLine("All payment-method integration checks passed.");
}
finally
{
    SqlConnection.ClearAllPools();
    // database is generated above, never supplied by the caller.
    masterCommand.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
    await masterCommand.ExecuteNonQueryAsync();
}
