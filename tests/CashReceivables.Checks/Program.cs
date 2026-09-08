using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GuardeSoftwareAPI.Controllers;
using GuardeSoftwareAPI.Services.activityLog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Cash;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Uses only a newly created disposable database; never reads the application's connection string.
var server = args.FirstOrDefault() ?? @".\SQLEXPRESS";
var database = "GuardeReceivablesTest_" + Guid.NewGuid().ToString("N");
var connectionString = new SqlConnectionStringBuilder {
    DataSource = server, InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true
};
using var master = new SqlConnection(connectionString.ConnectionString);
await master.OpenAsync();
using var masterCommand = master.CreateCommand();
masterCommand.CommandText = $"CREATE DATABASE [{database}]";
await masterCommand.ExecuteNonQueryAsync();
int checks = 0;
void Check(bool condition, string name) {
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); checks++;
}
async Task Reject(Func<Task> action, string name) {
    try { await action(); }
    catch (InvalidOperationException) { Check(true, name); return; }
    catch (KeyNotFoundException) { Check(true, name); return; }
    throw new Exception("FAIL: should reject " + name);
}
bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), true);

try {
    connectionString.InitialCatalog = database;
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["ConnectionStrings:DefaultConnection"] = connectionString.ConnectionString
    }).Build();
    var db = new AccessDB(config);
    var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migration.sql"));
    // Simulate the first draft of the feature so the unification migration is exercised.
    await db.ExecuteCommandAsync(@"
        CREATE TABLE dbo.cash_receivables (
            id INT IDENTITY(1,1) PRIMARY KEY,
            debtor NVARCHAR(150) NOT NULL,
            concept NVARCHAR(250) NOT NULL,
            agreement_date DATE NOT NULL,
            total_amount DECIMAL(18,2) NOT NULL,
            notes NVARCHAR(1000) NOT NULL CONSTRAINT DF_old_cash_receivables_notes DEFAULT N'',
            CONSTRAINT CK_cash_receivables_total CHECK (total_amount > 0),
            CONSTRAINT CK_cash_receivables_debtor CHECK (LEN(LTRIM(RTRIM(debtor))) > 0),
            CONSTRAINT CK_cash_receivables_concept CHECK (LEN(LTRIM(RTRIM(concept))) > 0)
        );
        CREATE TABLE dbo.cash_receivable_payments (
            id INT IDENTITY(1,1) PRIMARY KEY,
            receivable_id INT NOT NULL,
            payment_date DATE NOT NULL,
            amount DECIMAL(18,2) NOT NULL,
            comment NVARCHAR(500) NOT NULL CONSTRAINT DF_old_cash_receivable_payments_comment DEFAULT N'',
            request_id UNIQUEIDENTIFIER NOT NULL,
            CONSTRAINT FK_old_cash_receivable_payments_account FOREIGN KEY (receivable_id) REFERENCES dbo.cash_receivables(id),
            CONSTRAINT CK_old_cash_receivable_payments_amount CHECK (amount > 0),
            CONSTRAINT UQ_old_cash_receivable_payments_request UNIQUE(request_id)
        );
        INSERT INTO dbo.cash_receivables(debtor, concept, agreement_date, total_amount) VALUES (N'Juan', N'Celular', '20260801', 300.50);
    ");
    await db.ExecuteCommandAsync(migration);
    await db.ExecuteCommandAsync(migration);
    Check(true, "migration applies twice without losing schema");
    var dao = new DaoCashReceivables(db);
    var migrated = (await dao.GetAsync()).Single();
    Check(migrated.Description == "Juan · Celular", "old persona and concept are unified into one description");
    var agreement = new CashReceivableInput { Description = " Juan · Celular ", Date = new DateTime(2026, 8, 1), TotalAmount = 300.50m, Notes = "3 cuotas" };
    Check(!Valid(new CashReceivableInput { Description = " ", Date = agreement.Date, TotalAmount = 1 }), "blank description rejected");
    Check(!Valid(new CashReceivableInput { Description = "Juan · Celular", Date = agreement.Date, TotalAmount = 0.001m }), "sub-cent total rejected");
    Check(!Valid(new CashReceivablePaymentInput { Date = agreement.Date, Amount = 0, RequestId = Guid.NewGuid() }), "zero payment rejected");
    Check(!Valid(new CashReceivablePaymentInput { Date = agreement.Date, Amount = 1.001m, RequestId = Guid.NewGuid() }), "sub-cent payment rejected");
    Check(!Valid(new CashReceivablePaymentInput { Date = agreement.Date, Amount = 1 }), "missing retry key rejected");
    int id = await dao.CreateAsync(agreement);
    var row = (await dao.GetAsync()).Single(a => a.Id == id);
    Check(row.Description == "Juan · Celular" && row.RemainingAmount == 300.50m && row.Payments.Count == 0, "unified account has full pending balance");
    var first = new CashReceivablePaymentInput { Date = new DateTime(2026, 9, 1), Amount = 100.25m, Comment = "Cuota 1", RequestId = Guid.NewGuid() };
    int paymentId = await dao.AddPaymentAsync(id, first);
    int retryId = await dao.AddPaymentAsync(id, first);
    row = (await dao.GetAsync()).Single(a => a.Id == id);
    Check(paymentId == retryId && row.Payments.Count == 1 && row.PaidAmount == 100.25m && row.RemainingAmount == 200.25m, "partial payment survives across months and retry does not duplicate it");
    await Reject(() => dao.AddPaymentAsync(id, new CashReceivablePaymentInput { Date = first.Date, Amount = 201, RequestId = Guid.NewGuid() }), "overpayment");
    await Reject(() => dao.AddPaymentAsync(id, new CashReceivablePaymentInput { Date = agreement.Date.AddDays(-1), Amount = 1, RequestId = Guid.NewGuid() }), "payment before agreement");
    await Reject(() => dao.DeleteAsync(id), "delete account with payments");
    agreement.TotalAmount = 99;
    await Reject(() => dao.UpdateAsync(id, agreement), "total below collected amount");
    agreement.TotalAmount = 300.50m;
    agreement.Date = first.Date.AddDays(1);
    await Reject(() => dao.UpdateAsync(id, agreement), "agreement date after existing payment");
    agreement.Date = new DateTime(2026, 8, 1);
    agreement.Description = "Juan · Celular actualizado";
    await dao.UpdateAsync(id, agreement);
    Check((await dao.GetAsync()).Single(a => a.Id == id).Description == agreement.Description, "edit preserves payment history");
    int finalPayment = await dao.AddPaymentAsync(id, new CashReceivablePaymentInput { Date = first.Date.AddMonths(1), Amount = 200.25m, RequestId = Guid.NewGuid() });
    Check((await dao.GetAsync()).Single(a => a.Id == id).RemainingAmount == 0, "exact payoff closes account");
    await dao.DeletePaymentAsync(id, finalPayment);
    Check((await dao.GetAsync()).Single(a => a.Id == id).RemainingAmount == 200.25m, "deleting payment reopens balance");
    await dao.DeletePaymentAsync(id, paymentId);
    await dao.DeleteAsync(id);
    Check((await dao.GetAsync()).All(a => a.Id != id), "empty account can be deleted");

    agreement.TotalAmount = 100;
    id = await dao.CreateAsync(agreement);
    async Task<bool> ConcurrentPayment() {
        try { await dao.AddPaymentAsync(id, new CashReceivablePaymentInput { Date = first.Date, Amount = 70, RequestId = Guid.NewGuid() }); return true; }
        catch (InvalidOperationException) { return false; }
    }
    var results = await Task.WhenAll(ConcurrentPayment(), ConcurrentPayment());
    row = (await dao.GetAsync()).Single(a => a.Id == id);
    Check(results.Count(r => r) == 1 && row.PaidAmount == 70 && row.RemainingAmount == 30, "concurrent payments cannot exceed total");
    int otherId = await dao.CreateAsync(agreement);
    await Reject(() => dao.DeletePaymentAsync(otherId, row.Payments.Single().Id), "payment cannot be deleted through another account");
    // Minimal host: real routes, model validation and authorization, no application jobs.
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddControllers().AddApplicationPart(typeof(CashReceivablesController).Assembly);
    builder.Services.AddSingleton(db);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
    builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => {});
    builder.Services.AddAuthorization();
    await using var app = builder.Build();
    app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
    await app.StartAsync();
    try {
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        Check((await client.GetAsync("api/cashflow/receivables")).StatusCode == HttpStatusCode.Unauthorized, "API rejects unauthenticated access");
        client.DefaultRequestHeaders.Add("X-Test-UserType", "2");
        Check((await client.GetAsync("api/cashflow/receivables")).StatusCode == HttpStatusCode.Forbidden, "API rejects non-admin user");
        client.DefaultRequestHeaders.Remove("X-Test-UserType");
        client.DefaultRequestHeaders.Add("X-Test-UserType", "1");
        Check((await client.GetAsync("api/cashflow/receivables")).StatusCode == HttpStatusCode.OK, "API accepts business administrator claim");
        var created = await client.PostAsJsonAsync("api/cashflow/receivables", agreement);
        Check(created.StatusCode == HttpStatusCode.OK, "API creates validated account");
        int apiId = await created.Content.ReadFromJsonAsync<int>();
        var apiPayment = new CashReceivablePaymentInput { Date = first.Date, Amount = 100, RequestId = Guid.NewGuid() };
        Check((await client.PostAsJsonAsync($"api/cashflow/receivables/{apiId}/payments", apiPayment)).StatusCode == HttpStatusCode.OK, "API registers full payment");
        apiPayment.RequestId = Guid.NewGuid();
        Check((await client.PostAsJsonAsync($"api/cashflow/receivables/{apiId}/payments", apiPayment)).StatusCode == HttpStatusCode.Conflict, "API returns conflict for overpayment");
        apiPayment.Amount = 0;
        Check((await client.PostAsJsonAsync($"api/cashflow/receivables/{apiId}/payments", apiPayment)).StatusCode == HttpStatusCode.BadRequest, "API rejects zero payment before persistence");
        Check((await client.DeleteAsync("api/cashflow/receivables/2147483647")).StatusCode == HttpStatusCode.NotFound, "API reports missing account");
    } finally { await app.StopAsync(); }
    Console.WriteLine($"Completed {checks} checks.");
} finally {
    SqlConnection.ClearAllPools();
    // The identifier is generated above, never supplied by callers.
    masterCommand.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]";
    await masterCommand.ExecuteNonQueryAsync();
    Console.WriteLine("Disposable test database removed.");
}

// Only this isolated test host registers this handler; production uses JWT authentication.
sealed class TestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) {}
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserType", out var type)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Test"), new Claim("businessUserTypeId", type.ToString()) }, "Test");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
    }
}
