using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Cash;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Usa exclusivamente una base temporal; nunca lee la conexión de la aplicación.
var server = args.FirstOrDefault() ?? @".\SQLEXPRESS";
var database = "GuardeCashOrderingTest_" + Guid.NewGuid().ToString("N");
var connectionString = new SqlConnectionStringBuilder
{
    DataSource = server,
    InitialCatalog = "master",
    IntegratedSecurity = true,
    TrustServerCertificate = true
};

using var master = new SqlConnection(connectionString.ConnectionString);
await master.OpenAsync();
using var masterCommand = master.CreateCommand();
masterCommand.CommandText = $"CREATE DATABASE [{database}]";
await masterCommand.ExecuteNonQueryAsync();

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    checks++;
}

try
{
    connectionString.InitialCatalog = database;
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = connectionString.ConnectionString
    }).Build();
    var db = new AccessDB(config);
    await db.ExecuteCommandAsync(@"
        CREATE TABLE dbo.cash_flow_items (
            item_id INT IDENTITY(1,1) PRIMARY KEY,
            month INT NOT NULL,
            year INT NOT NULL,
            description NVARCHAR(255) NULL,
            display_order INT NOT NULL
        );

        INSERT dbo.cash_flow_items(month, year, description, display_order) VALUES
            (9, 2026, N'Concepto repetido', 1),
            (9, 2026, N'Concepto repetido', 2),
            (9, 2026, N'Otro concepto', 3),
            (10, 2026, N'Otro concepto', 3);
    ");

    var dao = new CashDao(db);
    await dao.UpdateItemsOrderAsync([
        new CashItemOrderDto { Id = 1, DisplayOrder = 3 },
        new CashItemOrderDto { Id = 2, DisplayOrder = 1 },
        new CashItemOrderDto { Id = 3, DisplayOrder = 2 }
    ]);

    var rows = await db.GetTableAsync("CashOrder", @"
        SELECT item_id, month, display_order
        FROM dbo.cash_flow_items
        ORDER BY item_id;
    ");

    Check(Convert.ToInt32(rows.Rows[0]["display_order"]) == 3,
        "first repeated concept keeps its item-specific order");
    Check(Convert.ToInt32(rows.Rows[1]["display_order"]) == 1,
        "second repeated concept does not overwrite its namesake");
    Check(Convert.ToInt32(rows.Rows[2]["display_order"]) == 2,
        "other current-month concept is reordered by id");
    Check(Convert.ToInt32(rows.Rows[3]["display_order"]) == 2,
        "replicated concept keeps its order in future months");

    Console.WriteLine($"Completed {checks} checks.");
}
finally
{
    masterCommand.CommandText = $@"
        ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        DROP DATABASE [{database}];";
    await masterCommand.ExecuteNonQueryAsync();
    Console.WriteLine("Disposable test database removed.");
}
