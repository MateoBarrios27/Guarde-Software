using System.Data;
using System.Reflection;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.AccountMovement;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Hubs;
using GuardeSoftwareAPI.Services.payment;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Services.accountMovement;
using GuardeSoftwareAPI.Services.rental;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using GuardeSoftwareAPI.Services.paymentMethod;
using GuardeSoftwareAPI.Services.activityLog;
using GuardeSoftwareAPI.Services.sync;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

int checks = 0;
void Check(bool pass, string name)
{
    if (!pass) throw new Exception("FAIL " + name);
    checks++;
    Console.WriteLine("PASS " + name);
}
AccountMovement Movement(int id, string date, string concept, decimal amount, bool credit = false) => new()
{
    Id = id, RentalId = 1, MovementDate = DateTime.Parse(date), Concept = concept,
    MovementType = credit ? "CREDITO" : "DEBITO", Amount = amount, PaymentId = credit ? id : null
};
List<AccountMovement> Ariadna(decimal payment) =>
[
    Movement(1, "2026-08-01", "Interés por mora de Julio 2026", 23200),
    Movement(2, "2026-08-01", "Alquiler Agosto 2026", 232000),
    Movement(3, "2026-09-01", "Interés por mora de Agosto 2026", 25500),
    Movement(4, "2026-09-01", "Alquiler Septiembre 2026", 232000),
    Movement(5, "2026-09-14", "Pago", payment, true)
];
foreach (var (payment, previous, interest, rent) in new[]
{
    (170000m, 62000m, 48700m, 232000m),
    (232000m, 0m, 48700m, 232000m),
    (232001m, 0m, 48699m, 232000m),
    (270000m, 0m, 10700m, 232000m),
    (280700m, 0m, 0m, 232000m),
    (300000m, 0m, 0m, 212700m),
    (512700m, 0m, 0m, 0m)
})
{
    var result = PaymentAllocationEngine.Allocate(Ariadna(payment));
    Check(result.Rows[0].UnpaidRent == previous, $"{payment}: prior principal {previous}");
    Check(result.Rows.Sum(r => r.UnpaidInterests) == interest, $"{payment}: interests {interest}");
    Check(result.Rows[1].UnpaidRent == rent, $"{payment}: September rent {rent}");
    Check(result.Rows[^1].Balance - result.Rows[^1].Paid - result.Rows[^1].AdvancedPayment == 512700 - payment, "ledger conservation");
}
var sequential = Ariadna(170000);
sequential.Add(Movement(6, "2026-09-15", "Segundo pago", 100000, true));
var sequenceResult = PaymentAllocationEngine.Allocate(sequential);
Check(sequenceResult.Rows[0].UnpaidRent == 0 && sequenceResult.Rows.Sum(r => r.UnpaidInterests) == 10700,
    "second payment closes 62000 capital before paying 38000 interests");
Check(PaymentAllocationEngine.Allocate(sequential.Where(m => m.Id != 6)).Rows[0].UnpaidRent == 62000,
    "deleting a payment rebuilds the original allocation");
var withFuture = Ariadna(170000);
withFuture.Add(Movement(6, "2026-10-01", "Alquiler Octubre 2026", 300000));
Check(PaymentAllocationEngine.Allocate(withFuture).Rows[0].UnpaidRent == 62000,
    "future rent does not change what the September payment covered");
var advance = PaymentAllocationEngine.Allocate([
    Movement(1, "2026-09-01", "Alquiler Septiembre 2026", 100),
    Movement(2, "2026-10-01", "Alquiler Octubre 2026", 100),
    Movement(3, "2026-09-10", "Pago", 150, true)]);
Check(advance.Rows[1].AllocatedRent == 50 && advance.Rows[1].AdvancedPayment == 50, "advance classification");
var creditBalance = PaymentAllocationEngine.Allocate(Ariadna(600000));
Check(creditBalance.UnallocatedCredits[5] == 87300 &&
    creditBalance.Rows[^1].Balance - creditBalance.Rows[^1].Paid - creditBalance.Rows[^1].AdvancedPayment == -87300,
    "surplus is preserved");
var multiMonth = PaymentAllocationEngine.Allocate([
    Movement(1, "2026-07-01", "Interés por mora", 10),
    Movement(2, "2026-07-01", "Alquiler Julio 2026", 100),
    Movement(3, "2026-08-01", "Interés por mora", 20),
    Movement(4, "2026-08-01", "Alquiler Agosto 2026", 100),
    Movement(5, "2026-09-01", "Alquiler Septiembre 2026", 100),
    Movement(6, "2026-09-14", "Pago", 150, true)]);
Check(multiMonth.Rows[0].UnpaidRent == 0 && multiMonth.Rows[1].UnpaidRent == 50 &&
    multiMonth.Rows.Sum(r => r.UnpaidInterests) == 30, "all prior months' capital precedes all interests");
var historical = PaymentAllocationEngine.Allocate([
    Movement(1, "2026-08-01", "Interés por mora", 10),
    Movement(2, "2026-08-01", "Alquiler Agosto 2026", 100),
    Movement(3, "2026-08-05", "Pago", 20, true),
    Movement(4, "2026-09-01", "Alquiler Septiembre 2026", 100),
    Movement(5, "2026-09-10", "Pago", 50, true)]);
Check(historical.Rows[0].AllocatedInterests == 10 && historical.Rows[0].UnpaidRent == 40,
    "same-month payment pays interest first; later payment pays remaining prior capital");

var lateFeeBeforePayment = PaymentAllocationEngine.Allocate(Ariadna(0)).Rows;
var lateFeeAtCutoff = LatePaymentSurchargeCalculator.Project(
    lateFeeBeforePayment, new DateTime(2026, 9, 14), 232000m);
Check(lateFeeAtCutoff.UnpaidInterestsAtCutoff == 48700m &&
      lateFeeAtCutoff.TaxableBase == 280700m &&
      lateFeeAtCutoff.SurchargeAmount == 28000m,
    "late fee freezes current rent and every unpaid interest at the cutoff");

// Only the local SQL Server and a uniquely named disposable database; no appsettings.
var database = "GuardeWaterfallChecks_" + Guid.NewGuid().ToString("N");
var builder = new SqlConnectionStringBuilder
{
    DataSource = @".\SQLEXPRESS", InitialCatalog = "master", IntegratedSecurity = true,
    TrustServerCertificate = true, ConnectTimeout = 5
};
using var master = new SqlConnection(builder.ConnectionString);
await master.OpenAsync();
using var admin = master.CreateCommand();
admin.CommandText = $"CREATE DATABASE [{database}]";
await admin.ExecuteNonQueryAsync();
try
{
    builder.InitialCatalog = database;
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = builder.ConnectionString }).Build();
    var db = new AccessDB(config);
    var balances = new ClientMonthBalanceService(db);
    await db.ExecuteCommandAsync("""
        CREATE TABLE clients(client_id INT PRIMARY KEY, full_name VARCHAR(200), payment_identifier DECIMAL(18,2),
            registration_date DATETIME, dni VARCHAR(30), cuit VARCHAR(30), preferred_payment_method_id INT,
            iva_condition VARCHAR(30), notes VARCHAR(200), departure_status VARCHAR(30), billing_type_id INT,
            increase_frequency_months INT, is_six_month_promotion BIT, initial_amount DECIMAL(18,2), active BIT, is_deleted BIT NOT NULL DEFAULT 0);
        CREATE TABLE rentals(rental_id INT IDENTITY PRIMARY KEY, client_id INT, start_date DATETIME, end_date DATETIME,
            contracted_m3 DECIMAL(18,2), months_unpaid INT, active BIT DEFAULT 1, price_lock_end_date DATETIME,
            occupied_spaces INT, increase_anchor_date DATETIME, pending_surcharge DECIMAL(18,2) DEFAULT 0,
            pending_surcharge_rent_base DECIMAL(18,2), pending_surcharge_period DATE);
        CREATE TABLE payment_methods(payment_method_id INT PRIMARY KEY, name VARCHAR(100), commission DECIMAL(18,2), active BIT);
        CREATE TABLE payments(payment_id INT IDENTITY PRIMARY KEY, client_id INT, payment_method_id INT, payment_date DATETIME, amount DECIMAL(18,2));
        CREATE TABLE rental_amount_history(rental_amount_history_id INT IDENTITY PRIMARY KEY, rental_id INT, amount DECIMAL(18,2), start_date DATETIME, end_date DATETIME);
        CREATE TABLE monthly_increase_settings(increase_setting_id INT IDENTITY PRIMARY KEY, effective_date DATE NOT NULL UNIQUE, percentage DECIMAL(5,2) NOT NULL);
        CREATE TABLE account_movements(movement_id INT IDENTITY PRIMARY KEY, rental_id INT, movement_date DATETIME, movement_type VARCHAR(10), concept VARCHAR(255), amount DECIMAL(18,2), payment_id INT NULL);
        CREATE TABLE client_month_balances(id INT IDENTITY PRIMARY KEY, rental_id INT, month_year VARCHAR(7), previous_balance DECIMAL(18,2), interests DECIMAL(18,2), monthly_debits DECIMAL(18,2), balance DECIMAL(18,2), paid DECIMAL(18,2), advanced_payment DECIMAL(18,2));
        CREATE TABLE lockers(locker_id INT PRIMARY KEY, identifier VARCHAR(30), rental_id INT, warehouse_id INT, locker_type_id INT, active BIT);
        CREATE TABLE rental_lockers(rental_id INT, locker_id INT);
        CREATE TABLE client_locker_history(client_id INT, locker_id INT, start_date DATETIME, end_date DATETIME);
        CREATE TABLE warehouses(warehouse_id INT PRIMARY KEY, name VARCHAR(100));
        CREATE TABLE emails(email_id INT IDENTITY PRIMARY KEY, client_id INT, address VARCHAR(255), active BIT);
        ALTER TABLE clients ADD color VARCHAR(30), receive_communications BIT, comment VARCHAR(500), comment_updated_at DATETIME;
        CREATE TABLE addresses(address_id INT IDENTITY PRIMARY KEY, client_id INT, street VARCHAR(200), city VARCHAR(100), province VARCHAR(100));
        CREATE TABLE billing_types(billing_type_id INT PRIMARY KEY, name VARCHAR(100));
        INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount, active, increase_frequency_months) VALUES(1, 'Ariadna prueba', 5.13, 1, 232000, 1, 3);
        SET IDENTITY_INSERT rentals ON; INSERT rentals(rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active, price_lock_end_date, occupied_spaces, increase_anchor_date, pending_surcharge, pending_surcharge_rent_base, pending_surcharge_period) VALUES(1, 1, '2020-01-01', NULL, 1, 2, 1, NULL, 1, '2026-10-01', 0, NULL, NULL);
        INSERT payment_methods VALUES(1, 'MP', 0, 1);
        INSERT rental_amount_history VALUES(1, 232000, '2020-01-01', NULL);
        """);
    var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migration.sql"));
    await db.ExecuteCommandAsync(migration);
    await db.ExecuteCommandAsync(migration);
    Check(true, "migration applies twice");
    var collectionMigration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "collection-migration.sql"));
    for (int pass = 0; pass < 2; pass++)
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(collectionMigration, @"(?im)^GO\s*$"))
            if (!string.IsNullOrWhiteSpace(batch)) await db.ExecuteCommandAsync(batch);
    Check(true, "collection decision migration applies twice");

    if (args.Contains("--client-search-only"))
    {
        await ClientSearchChecks.RunAsync(db, Check);
        Console.WriteLine($"ALL {checks} CHECKS PASSED");
        return;
    }

    async Task<decimal> Scalar(string sql) => Convert.ToDecimal(await db.ExecuteScalarAsync(sql));
    async Task Insert(AccountMovement m) => await db.ExecuteCommandAsync("""
        INSERT account_movements(rental_id, movement_date, movement_type, concept, amount, payment_id)
        VALUES(1, @date, @type, @concept, @amount, @payment)
        """, [new SqlParameter("@date",m.MovementDate), new SqlParameter("@type",m.MovementType),
        new SqlParameter("@concept",m.Concept), new SqlParameter("@amount",m.Amount), new SqlParameter("@payment",(object?)m.PaymentId ?? DBNull.Value)]);
    // Migration compatibility: legacy paid rows must not suddenly expose all interest as unpaid.
    await db.ExecuteCommandAsync("INSERT client_month_balances(rental_id,month_year,previous_balance,interests,monthly_debits,balance,paid,advanced_payment) VALUES(1,'08/2026',0,23200,232000,255200,170000,0)");
    Check(await Scalar("SELECT unpaid_interests FROM client_month_balances") == 0 &&
        await Scalar("SELECT unpaid_rent FROM client_month_balances") == 85200, "legacy fallback preserves existing breakdown until rebuild");
    foreach (var m in Ariadna(0).Where(m => m.MovementType == "DEBITO")) await Insert(m);
    await balances.RebuildForRentalAsync(1);
    var activity = DispatchProxy.Create<IActivityLogService, ActivityStub>();
    var historyService = new RentalAmountHistoryService(db, activity);
    var accountService = new AccountMovementService(db, NullLogger<AccountMovementService>.Instance, balances, null!, historyService);
    var stateService = new PaymentStateService(db);
    var payments = new PaymentService(db, accountService, NullLogger<PaymentService>.Instance,
        new RentalService(db), historyService, new PaymentMethodService(db, activity), balances,
        stateService, new PaymentPresenceRegistry(), activity);
    async Task Pay(decimal amount, int day, bool skipFuture = true, string? action = null) {
        var dto = new CreatePaymentTransaction {
            ClientId = 1, PaymentMethodId = 1, Amount = amount, Date = new DateTime(2026, 9, day),
            SkipFutureProjection = skipFuture, SurchargeAction = action,
            ExpectedPaymentStateToken = (await stateService.GetSnapshotAsync(1)).Token
        };
        try { await payments.CreatePaymentWithMovementAsync(dto); }
        catch (PaymentDecisionRequiredException ex) {
            Check(ex.Details.Scenario == "partial_payment", "prior principal partial payment consults collection month");
            dto.FutureDebitAction = ex.Details.Options.Any(o=>o.Action=="close_partial_month")
                ? "close_partial_month" : "keep_month_pending";
            dto.PaymentDecisionToken = ex.Details.DecisionToken;
            await payments.CreatePaymentWithMovementAsync(dto);
        }
    }
    await Pay(170000,14, skipFuture:false);
    Check(await Scalar("SELECT unpaid_rent FROM client_month_balances WHERE month_year='08/2026'") == 62000, "real payment: previous principal 62000");
    Check(await Scalar("SELECT SUM(unpaid_interests) FROM client_month_balances") == 48700, "real payment: interests untouched");
    Check(await Scalar("SELECT TOP 1 balance-paid-advanced_payment FROM client_month_balances ORDER BY id DESC") == 342700, "real payment: debt 342700");
    Check(await Scalar("SELECT COUNT(*) FROM account_movements WHERE concept LIKE 'Alquiler Octubre%'") == 0, "prior principal payment does not project October rent");
    var concept = (await db.ExecuteScalarAsync("SELECT concept FROM account_movements WHERE movement_type='CREDITO'"))?.ToString();
    Check(concept!.Contains("Alquiler Agosto 2026") && !concept.Contains("Interés"), "real payment description only names August principal");
    var clients = await new DaoClient(db).GetClients();
    Check(Convert.ToDecimal(clients.Rows[0]["PreviousBalance"]) == -62000 &&
        Convert.ToDecimal(clients.Rows[0]["interest_amount"]) == 48700 &&
        Convert.ToDecimal(clients.Rows[0]["balance"]) == -342700, "client list SQL uses the actual allocation");
    var financial = await new CommunicationDao(db).GetClientFinancialData(1);
    Check(financial.PreviousBalance == 62000 && financial.Surcharge == 48700,
        "account statements agree with client capital and interest");
    var snapshot = await new SyncService(db, payments, NullLogger<SyncService>.Instance).GetSnapshotAsync();
    Check(snapshot.Clients[0].PreviousBalance == -62000 && snapshot.Clients[0].InterestAmount == 48700,
        "offline snapshot agrees with client capital and interest");
    var pending = await new DaoRental(db).GetPendingPaymentsAsync();
    Check(Convert.ToDecimal(pending.Rows[0]["PreviousBalance"]) == -62000 &&
        Convert.ToDecimal(pending.Rows[0]["InterestAmount"]) == 48700 &&
        Convert.ToDecimal(pending.Rows[0]["balance"]) == -342700, "dashboard uses full debt, excluding interests from prior principal");
    var late = await new DaoRental(db).GetAllActiveRentalsWithStatusAsync();
    Check(Convert.ToDecimal(late.Rows[0]["CurrentInterests"]) == 48700 &&
        Convert.ToDecimal(late.Rows[0]["UnpaidMonthlyDebits"]) == 232000, "interest job bases use actual component balances");
    await balances.RebuildForRentalAsync(1);
    Check(await Scalar("SELECT SUM(unpaid_interests) FROM client_month_balances") == 48700, "rebuild is idempotent");
    await Pay(100000,15);
    Check(await Scalar("SELECT unpaid_rent FROM client_month_balances WHERE month_year='08/2026'") == 0 &&
        await Scalar("SELECT SUM(unpaid_interests) FROM client_month_balances") == 10700, "real second payment follows waterfall");
    using (var cn = db.GetConnectionClose())
    {
        await cn.OpenAsync();
        using var tx = cn.BeginTransaction();
        using var remove = new SqlCommand("DELETE account_movements WHERE payment_id=(SELECT MAX(payment_id) FROM payments)",cn,tx);
        await remove.ExecuteNonQueryAsync();
        await balances.RebuildForRentalTransactionAsync(1,cn,tx);
        await tx.RollbackAsync();
    }
    Check(await Scalar("SELECT SUM(unpaid_interests) FROM client_month_balances") == 10700, "transaction rollback preserves allocations");
    await db.ExecuteCommandAsync("DELETE account_movements WHERE payment_id=(SELECT MAX(payment_id) FROM payments); DELETE payments WHERE payment_id=(SELECT MAX(payment_id) FROM payments)");
    await balances.RebuildForRentalAsync(1);
    Check(await Scalar("SELECT unpaid_rent FROM client_month_balances WHERE month_year='08/2026'") == 62000 &&
        await Scalar("SELECT SUM(unpaid_interests) FROM client_month_balances") == 48700, "deletion rebuild restores 62000 capital and 48700 interests");
    await db.ExecuteCommandAsync("UPDATE rentals SET pending_surcharge=28000, pending_surcharge_rent_base=232000, pending_surcharge_period='2026-09-01'");
    await Pay(280700,16, action:"next_payment");
    Check(await Scalar("SELECT amount FROM account_movements WHERE concept='Interés por mora de Septiembre 2026'") == 28000,
        "late payment keeps the cutoff fee even when it later pays every prior interest");

    // NextPaymentDay: a pending plan starts in the current month; any formal
    // partial payment defers the next contact one month, while complete advance
    // coverage defers it through the last fully paid rent month.
    await db.ExecuteCommandAsync("UPDATE clients SET active=0 WHERE client_id=1; UPDATE rentals SET active=0 WHERE rental_id=1");
    var todayAr = DateTime.UtcNow.AddHours(-3);
    var thisMonth = new DateTime(todayAr.Year, todayAr.Month, 1);
    var previousMonth = thisMonth.AddMonths(-1);
    await db.ExecuteCommandAsync("""
        INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount,
            active, increase_frequency_months, registration_date, receive_communications)
        VALUES(2, 'Cliente planificación', 9.99, 1, 100, 1, 4, @previousMonth, 1);
        SET IDENTITY_INSERT rentals ON; INSERT rentals(rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active,
            price_lock_end_date, occupied_spaces, increase_anchor_date, pending_surcharge,
            pending_surcharge_rent_base, pending_surcharge_period)
        VALUES(2, 2, @previousMonth, NULL, 1, 0, 1, NULL, 1, NULL, 0, NULL, NULL);
        INSERT rental_amount_history(rental_id, amount, start_date, end_date)
        VALUES(2, 100, @previousMonth, NULL);
        """, [new SqlParameter("@previousMonth", previousMonth)]);

    async Task AddRentDebit(int rentalId, DateTime month, bool planned, decimal amount = 100m)
    {
        await db.ExecuteCommandAsync("""
            INSERT account_movements(rental_id, movement_date, movement_type, concept, amount, payment_id)
            VALUES(@rentalId, @month, 'DEBITO', @concept, @amount, NULL)
            """, [
                new SqlParameter("@rentalId", rentalId),
                new SqlParameter("@month", month),
                new SqlParameter("@concept", $"Alquiler {month:MM/yyyy}" + (planned ? " (Planificado)" : "")),
                new SqlParameter("@amount", amount)
            ]);
    }

    async Task AddFormalPayment(int clientId, int rentalId, DateTime date, decimal amount)
    {
        var paymentId = Convert.ToInt32(await db.ExecuteScalarAsync("""
            INSERT payments(client_id, payment_method_id, payment_date, amount)
            VALUES(@clientId, 1, @date, @amount);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, [
                new SqlParameter("@clientId", clientId),
                new SqlParameter("@date", date),
                new SqlParameter("@amount", amount)
            ]));
        await db.ExecuteCommandAsync("""
            INSERT account_movements(rental_id, movement_date, movement_type, concept, amount, payment_id)
            VALUES(@rentalId, @date, 'CREDITO', 'Pago', @amount, @paymentId)
            """, [
                new SqlParameter("@rentalId", rentalId),
                new SqlParameter("@date", date),
                new SqlParameter("@amount", amount),
                new SqlParameter("@paymentId", paymentId)
            ]);
    }

    async Task<DateTime> ClientListNextPayment()
    {
        var table = await new DaoClient(db).GetClients();
        return Convert.ToDateTime(table.Select("client_id=2").Single()["next_payment_day"]);
    }

    async Task<DateTime> ClientDetailNextPayment()
    {
        var table = await new DaoClient(db).GetClientDetailByIdAsync(2);
        return Convert.ToDateTime(table.Rows[0]["next_payment_day"]);
    }

    async Task<DateTime> CommunicationSelectorNextPayment()
    {
        var clients = await new CommunicationDao(db).GetClientsForSelectorAsync();
        return clients.Single(c => c.Id == 2).NextPaymentDate
            ?? throw new Exception("Communication selector returned no next payment date");
    }

    bool SameMonth(DateTime actual, DateTime expected) =>
        actual.Year == expected.Year && actual.Month == expected.Month;

    await AddRentDebit(2, previousMonth, planned: false, amount: 150m);
    await AddFormalPayment(2, 2, previousMonth.AddDays(10), 100);
    await AddRentDebit(2, thisMonth, planned: true);
    await AddRentDebit(2, thisMonth.AddMonths(1), planned: true);
    await AddRentDebit(2, thisMonth.AddMonths(2), planned: true);
    await balances.RebuildForRentalAsync(2);

    Check(SameMonth(await ClientListNextPayment(), thisMonth),
        "pending current-through-future plan starts next payment in current month");
    Check(SameMonth(await ClientDetailNextPayment(), thisMonth),
        "client detail uses current month for an unpaid plan");
    Check(SameMonth(await CommunicationSelectorNextPayment(), thisMonth),
        "communication selector includes an unpaid current plan in the current month");
    var plannedPending = await new DaoRental(db).GetPendingPaymentsAsync();
    Check(SameMonth(Convert.ToDateTime(plannedPending.Select("client_id=2").Single()["NextPaymentDay"]), thisMonth),
        "dashboard uses current month for an unpaid plan");
    var plannedSnapshot = await new SyncService(db, payments, NullLogger<SyncService>.Instance).GetSnapshotAsync();
    Check(SameMonth(DateTime.Parse(plannedSnapshot.Clients.Single(c => c.Id == 2).NextPaymentDay!), thisMonth),
        "offline snapshot uses current month for an unpaid plan");
    Check(await new CashDao(db).GetPendingCollectionAsync(thisMonth.Month, thisMonth.Year) > 0,
        "cash attributes an unpaid current plan to the current month");

    await AddFormalPayment(2, 2, thisMonth.AddDays(9), 50);
    await balances.RebuildForRentalAsync(2);
    Check(SameMonth(await ClientListNextPayment(), thisMonth),
        "payment consumed by prior rent keeps next payment in current month");
    Check(SameMonth(await ClientDetailNextPayment(), thisMonth),
        "client detail stays in current month when current rent was untouched");
    Check(SameMonth(await CommunicationSelectorNextPayment(), thisMonth),
        "communication selector keeps a prior-only payment in the current month");
    var priorOnlyPending = await new DaoRental(db).GetPendingPaymentsAsync();
    Check(SameMonth(Convert.ToDateTime(priorOnlyPending.Select("client_id=2").Single()["NextPaymentDay"]), thisMonth),
        "dashboard stays in current month when a payment only closes prior rent");
    var priorOnlySnapshot = await new SyncService(db, payments, NullLogger<SyncService>.Instance).GetSnapshotAsync();
    Check(SameMonth(DateTime.Parse(priorOnlySnapshot.Clients.Single(c => c.Id == 2).NextPaymentDay!), thisMonth),
        "offline snapshot stays in current month when current rent was untouched");
    Check(await new CashDao(db).GetPendingCollectionAsync(thisMonth.Month, thisMonth.Year) > 0,
        "cash keeps collection in current month when a payment only closes prior rent");

    await AddFormalPayment(2, 2, thisMonth.AddDays(10), 50);
    await balances.RebuildForRentalAsync(2);
    var monthAfterPayment = thisMonth.AddMonths(1);
    Check(SameMonth(await ClientListNextPayment(), monthAfterPayment),
        "partial payment that reaches current rent defers next payment to next month");
    Check(SameMonth(await ClientDetailNextPayment(), monthAfterPayment),
        "client detail defers after current rent was partially paid");
    Check(SameMonth(await CommunicationSelectorNextPayment(), monthAfterPayment),
        "communication selector defers after current rent was partially paid");
    var partialSnapshot = await new SyncService(db, payments, NullLogger<SyncService>.Instance).GetSnapshotAsync();
    Check(SameMonth(DateTime.Parse(partialSnapshot.Clients.Single(c => c.Id == 2).NextPaymentDay!), monthAfterPayment),
        "offline snapshot defers after current rent was partially paid");
    Check(await new CashDao(db).GetPendingCollectionAsync(thisMonth.Month, thisMonth.Year) == 0 &&
          await new CashDao(db).GetPendingCollectionAsync(monthAfterPayment.Month, monthAfterPayment.Year) > 0,
        "cash moves remaining debt after current rent was reached");

    await AddFormalPayment(2, 2, thisMonth.AddDays(11), 250);
    await balances.RebuildForRentalAsync(2);
    var monthAfterPlan = thisMonth.AddMonths(3);
    Check(SameMonth(await ClientListNextPayment(), monthAfterPlan),
        "complete advance coverage defers next payment beyond the plan");
    Check(SameMonth(await ClientDetailNextPayment(), monthAfterPlan),
        "client detail defers next payment beyond a fully paid plan");
    Check(SameMonth(await CommunicationSelectorNextPayment(), monthAfterPlan),
        "communication selector defers beyond a fully paid plan");
    var coveredSnapshot = await new SyncService(db, payments, NullLogger<SyncService>.Instance).GetSnapshotAsync();
    Check(SameMonth(DateTime.Parse(coveredSnapshot.Clients.Single(c => c.Id == 2).NextPaymentDay!), monthAfterPlan),
        "offline snapshot defers next payment beyond a fully paid plan");

    // A projected statement for the month when the next payment is due must
    // include the complete pending plan, not only the target month's debit.
    var projectedStatementMonth = thisMonth.AddMonths(1);
    await db.ExecuteCommandAsync("""
        INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount,
            active, increase_frequency_months, registration_date, receive_communications)
        VALUES(3, 'Cliente estado proyectado', 0, 1, 100, 1, 4, @thisMonth, 1);
        SET IDENTITY_INSERT rentals ON; INSERT rentals(rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active,
            price_lock_end_date, occupied_spaces, increase_anchor_date, pending_surcharge,
            pending_surcharge_rent_base, pending_surcharge_period)
        VALUES(3, 3, @thisMonth, NULL, 1, 0, 1, NULL, 1, NULL, 0, NULL, NULL);
        INSERT rental_amount_history(rental_id, amount, start_date, end_date)
        VALUES(3, 100, @thisMonth, NULL);
        """, [new SqlParameter("@thisMonth", thisMonth)]);

    await AddRentDebit(3, thisMonth, planned: false);
    await AddFormalPayment(3, 3, thisMonth.AddDays(5), 100);
    await AddRentDebit(3, projectedStatementMonth, planned: true);
    await AddRentDebit(3, projectedStatementMonth.AddMonths(1), planned: true);
    await AddRentDebit(3, projectedStatementMonth.AddMonths(2), planned: true);
    await balances.RebuildForRentalAsync(3);

    var projectedStatementRecipients = await new CommunicationDao(db).GetClientsForSelectorAsync();
    Check(SameMonth(
            projectedStatementRecipients.Single(c => c.Id == 3).NextPaymentDate
                ?? throw new Exception("Projected statement client returned no next payment date"),
            projectedStatementMonth),
        "projected statement client is due in the first month of the pending plan");

    var normalPlannedStatement = await new CommunicationDao(db).GetClientFinancialData(3);
    Check(normalPlannedStatement.CurrentBalance == 300m,
        "normal statement includes the complete three-month pending plan");

    var projectedPlannedStatement = await new CommunicationDao(db).GetClientFinancialData(3, isNextMonth: true);
    Check(projectedPlannedStatement.CurrentBalance == 300m,
        "projected statement includes the complete plan due in its target month");

    // A pending surcharge belongs to the statement even when the planned
    // debits include an assigned increase. It is not materialized as an
    // account movement until the payment workflow applies it.
    var increasedPlanMonth = projectedStatementMonth.AddMonths(1);
    await db.ExecuteCommandAsync("""
        INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount,
            active, increase_frequency_months, registration_date, receive_communications)
        VALUES(4, 'Cliente plan con aumento y recargo', 0, 1, 100, 1, 4, @thisMonth, 1);
        SET IDENTITY_INSERT rentals ON; INSERT rentals(rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active,
            price_lock_end_date, occupied_spaces, increase_anchor_date, pending_surcharge,
            pending_surcharge_rent_base, pending_surcharge_period)
        VALUES(4, 4, @thisMonth, NULL, 1, 0, 1, NULL, 1, @increaseMonth, 25, 100, @thisMonth);
        INSERT rental_amount_history(rental_id, amount, start_date, end_date)
        VALUES(4, 100, @thisMonth, NULL);
        """, [
            new SqlParameter("@thisMonth", thisMonth),
            new SqlParameter("@increaseMonth", increasedPlanMonth)
        ]);

    await AddRentDebit(4, thisMonth, planned: false);
    await AddFormalPayment(4, 4, thisMonth.AddDays(5), 100);
    await balances.RebuildForRentalAsync(4);

    var increasedPlanContext = await accountService.GetPaymentPlanningContextAsync(4, 3);
    Check(increasedPlanContext.Increases.Count == 1 &&
          increasedPlanContext.Increases[0].Year == increasedPlanMonth.Year &&
          increasedPlanContext.Increases[0].Month == increasedPlanMonth.Month,
        "payment plan detects the assigned increase inside the planned period");

    var increasedPlan = await accountService.PlanClientPaymentAsync(new PlanClientPaymentDto
    {
        ClientId = 4,
        Months = 3,
        ChargeHalfSixthMonth = true,
        AppliedIncreases =
        [
            new PlannedPaymentIncreaseDto
            {
                Year = increasedPlanMonth.Year,
                Month = increasedPlanMonth.Month,
                Percentage = 10,
                NewRentAmount = 110
            }
        ]
    });
    Check(increasedPlan.TotalAmount == 320m,
        "payment plan applies the assigned increase to its month and following month");

    var normalIncreasedStatement = await new CommunicationDao(db).GetClientFinancialData(4);
    Check(normalIncreasedStatement.Surcharge == 25m && normalIncreasedStatement.CurrentBalance == 345m,
        "normal statement includes pending surcharge alongside the increased plan");

    var projectedIncreasedStatement = await new CommunicationDao(db).GetClientFinancialData(4, isNextMonth: true);
    Check(projectedIncreasedStatement.Surcharge == 25m && projectedIncreasedStatement.CurrentBalance == 345m,
        "projected statement includes pending surcharge alongside the increased plan");

    // Six months or more lock the base price even when an increase anchor falls
    // inside the period. The sixth-month benefit remains an explicit operator
    // choice for promotional clients.
    async Task AddSixMonthPromotionClient(int clientId)
    {
        await db.ExecuteCommandAsync("""
            INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount,
                active, increase_frequency_months, registration_date, receive_communications, is_six_month_promotion)
            VALUES(@clientId, 'Cliente promoción semestral', 0, 1, 100, 1, 4, @thisMonth, 1, 1);
            SET IDENTITY_INSERT rentals ON; INSERT rentals(rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active,
                price_lock_end_date, occupied_spaces, increase_anchor_date, pending_surcharge,
                pending_surcharge_rent_base, pending_surcharge_period)
            VALUES(@clientId, @clientId, @thisMonth, NULL, 1, 0, 1, NULL, 1, @increaseMonth, 0, NULL, NULL);
            INSERT rental_amount_history(rental_id, amount, start_date, end_date)
            VALUES(@clientId, 100, @thisMonth, NULL);
            """, [
                new SqlParameter("@clientId", clientId),
                new SqlParameter("@thisMonth", thisMonth),
                new SqlParameter("@increaseMonth", projectedStatementMonth.AddMonths(2))
            ]);
        await AddRentDebit(clientId, thisMonth, planned: false);
        await AddFormalPayment(clientId, clientId, thisMonth.AddDays(5), 100);
        await balances.RebuildForRentalAsync(clientId);
    }

    await AddSixMonthPromotionClient(5);
    var lockedPromotionContext = await accountService.GetPaymentPlanningContextAsync(5, 6);
    Check(lockedPromotionContext.IsPriceLocked && lockedPromotionContext.Increases.Count == 0,
        "six-month plan suppresses an increase anchor inside the planned period");

    var discountedPromotionPlan = await accountService.PlanClientPaymentAsync(new PlanClientPaymentDto
    {
        ClientId = 5,
        Months = 6,
        ChargeHalfSixthMonth = true
    });
    Check(discountedPromotionPlan.TotalAmount == 550m &&
          discountedPromotionPlan.Months.Take(5).All(month => month.Amount == 100m) &&
          discountedPromotionPlan.Months[5].Amount == 50m &&
          discountedPromotionPlan.Months[5].IsHalfPromotion,
        "promotional six-month plan charges half of only the sixth month");
    Check(Convert.ToDecimal(await db.ExecuteScalarAsync(
            "SELECT COUNT(*) FROM rental_amount_history WHERE rental_id=5 AND amount<>100")) == 0,
        "six-month price lock does not create an increased rent history");

    await AddSixMonthPromotionClient(6);
    var fullPricePromotionPlan = await accountService.PlanClientPaymentAsync(new PlanClientPaymentDto
    {
        ClientId = 6,
        Months = 6,
        ChargeHalfSixthMonth = false
    });
    Check(fullPricePromotionPlan.TotalAmount == 600m &&
          fullPricePromotionPlan.Months.All(month => month.Amount == 100m && !month.IsHalfPromotion),
        "operator can disable the sixth-month benefit and charge the full month");

    // The monthly debit job fills an increase that was not planned while the
    // preceding month remained completely unpaid. Partial payment is excluded.
    var automaticDebitMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    var previousAutomaticDebitMonth = automaticDebitMonth.AddMonths(-1);
    await db.ExecuteCommandAsync("""
        INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount,
            active, increase_frequency_months)
        VALUES(7, 'Cliente aumento automático', 0, 1, 100000, 1, 4),
              (8, 'Cliente con pago parcial', 0, 1, 100000, 1, 4);
        SET IDENTITY_INSERT rentals ON; INSERT rentals(rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active,
            price_lock_end_date, occupied_spaces, increase_anchor_date, pending_surcharge,
            pending_surcharge_rent_base, pending_surcharge_period)
        VALUES(7, 7, @start_date, NULL, 1, 0, 1, NULL, 1, @increase_month, 0, NULL, NULL),
              (8, 8, @start_date, NULL, 1, 0, 1, NULL, 1, @increase_month, 0, NULL, NULL);
        INSERT rental_amount_history(rental_id, amount, start_date, end_date)
        VALUES(7, 100000, @history_start, NULL), (8, 100000, @history_start, NULL);
        INSERT account_movements(rental_id, movement_date, movement_type, concept, amount, payment_id)
        VALUES(7, @previous_month, 'DEBITO', 'Alquiler mes anterior', 100000, NULL),
              (8, @previous_month, 'DEBITO', 'Alquiler mes anterior', 100000, NULL),
              (8, DATEADD(day, 5, @previous_month), 'CREDITO', 'Pago parcial', 1, 800);
        INSERT monthly_increase_settings(effective_date, percentage) VALUES(@increase_month, 10);
        """, [
            new SqlParameter("@start_date", previousAutomaticDebitMonth),
            new SqlParameter("@increase_month", automaticDebitMonth),
            new SqlParameter("@history_start", automaticDebitMonth.AddYears(-2)),
            new SqlParameter("@previous_month", previousAutomaticDebitMonth)
        ]);
    await balances.RebuildForRentalAsync(7);
    await balances.RebuildForRentalAsync(8);
    var monthlyDebits = new AccountMovementService(db, NullLogger<AccountMovementService>.Instance,
        balances, null!, historyService);
    await monthlyDebits.ApplyMonthlyDebitsAsync();
    Check(Convert.ToDecimal(await db.ExecuteScalarAsync(
            "SELECT amount FROM account_movements WHERE rental_id=7 AND movement_date >= @month AND concept LIKE 'Alquiler %'",
            [new SqlParameter("@month", automaticDebitMonth)])) == 110000m,
        "automatic monthly debit applies configured increase to fully unpaid missed plan");
    Check(Convert.ToDecimal(await db.ExecuteScalarAsync(
            "SELECT amount FROM rental_amount_history WHERE rental_id=7 AND start_date=@month",
            [new SqlParameter("@month", automaticDebitMonth)])) == 110000m &&
          Convert.ToDateTime(await db.ExecuteScalarAsync(
              "SELECT increase_anchor_date FROM rentals WHERE rental_id=7")).Date == automaticDebitMonth.AddMonths(3),
        "automatic increase creates new rent history and advances anchor by configured frequency");
    Check(Convert.ToDecimal(await db.ExecuteScalarAsync(
            "SELECT amount FROM account_movements WHERE rental_id=8 AND movement_date >= @month AND concept LIKE 'Alquiler %'",
            [new SqlParameter("@month", automaticDebitMonth)])) == 100000m &&
          Convert.ToInt32(await db.ExecuteScalarAsync(
              "SELECT COUNT(*) FROM rental_amount_history WHERE rental_id=8 AND start_date=@month",
              [new SqlParameter("@month", automaticDebitMonth)])) == 0 &&
          Convert.ToDateTime(await db.ExecuteScalarAsync(
              "SELECT increase_anchor_date FROM rentals WHERE rental_id=8")).Date == automaticDebitMonth,
        "automatic configured increase does not apply when prior rent was partially paid");

    var movementDao = new DaoAccountMovement(db);
    const string concurrentConcept = "Alquiler Enero 2099";
    using (var firstConnection = db.GetConnectionClose())
    using (var secondConnection = db.GetConnectionClose())
    {
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();
        using var firstTransaction = firstConnection.BeginTransaction();
        using var secondTransaction = secondConnection.BeginTransaction();

        Check(!await movementDao.IsDebitAlreadyCreatedAsync(99, concurrentConcept, firstConnection, firstTransaction),
            "first monthly debit transaction sees the period as available");

        var waitingDuplicateCheck = movementDao.IsDebitAlreadyCreatedAsync(
            99, concurrentConcept, secondConnection, secondTransaction);
        await Task.Delay(250);
        Check(!waitingDuplicateCheck.IsCompleted,
            "concurrent monthly debit check waits for the transaction holding the period lock");

        await movementDao.CreateAccountMovementTransactionAsync(new AccountMovement
        {
            RentalId = 99,
            MovementDate = new DateTime(2099, 1, 1),
            MovementType = "DEBITO",
            Concept = concurrentConcept,
            Amount = 100m
        }, firstConnection, firstTransaction);
        await firstTransaction.CommitAsync();

        Check(await waitingDuplicateCheck,
            "waiting monthly debit transaction observes the debit committed by its competitor");
        await secondTransaction.CommitAsync();
    }

    await PaymentDecisionChecks.RunAsync(db, payments, balances, stateService, Check);

    await DepartureChecks.RunAsync(db, balances, Check);
    await ReactivationChecks.RunAsync(db, balances, Check);

    Console.WriteLine($"ALL {checks} CHECKS PASSED");
}
finally
{
    SqlConnection.ClearAllPools();
    admin.CommandText = $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];";
    await admin.ExecuteNonQueryAsync();
    Console.WriteLine("Removed disposable database " + database);
}

public class ActivityStub : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        method?.Name == "TryCreateActivityLogAsync" ? Task.FromResult(true) : throw new NotSupportedException(method?.Name);
}
