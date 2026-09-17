using System.Data;
using System.Reflection;
using GuardeSoftwareAPI.Dao;
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
var lateFeeBlockedByPriorRent = LatePaymentSurchargeCalculator.Project(
    lateFeeBeforePayment, new DateTime(2026, 9, 14), 170000m, 232000m);
Check(lateFeeBlockedByPriorRent.UnpaidInterestsAfterPayment == 48700m &&
      lateFeeBlockedByPriorRent.TaxableBase == 280700m &&
      lateFeeBlockedByPriorRent.SurchargeAmount == 28000m,
    "late fee includes every interest still unpaid after prior rent");
var lateFeeAfterPartialInterestPayment = LatePaymentSurchargeCalculator.Project(
    lateFeeBeforePayment, new DateTime(2026, 9, 14), 250000m, 232000m);
Check(lateFeeAfterPartialInterestPayment.UnpaidInterestsAfterPayment == 30700m &&
      lateFeeAfterPartialInterestPayment.TaxableBase == 262700m &&
      lateFeeAfterPartialInterestPayment.SurchargeAmount == 26200m,
    "late fee includes only the interest remainder after the payment waterfall");
var lateFeeAfterFullInterestPayment = LatePaymentSurchargeCalculator.Project(
    lateFeeBeforePayment, new DateTime(2026, 9, 14), 280700m, 232000m);
Check(lateFeeAfterFullInterestPayment.UnpaidInterestsAfterPayment == 0m &&
      lateFeeAfterFullInterestPayment.TaxableBase == 232000m &&
      lateFeeAfterFullInterestPayment.SurchargeAmount == 23200m,
    "late fee uses only current rent when all previous interests were paid");

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
        CREATE TABLE rentals(rental_id INT PRIMARY KEY, client_id INT, start_date DATETIME, end_date DATETIME,
            contracted_m3 DECIMAL(18,2), months_unpaid INT, active BIT, price_lock_end_date DATETIME,
            occupied_spaces INT, increase_anchor_date DATETIME, pending_surcharge DECIMAL(18,2),
            pending_surcharge_rent_base DECIMAL(18,2), pending_surcharge_period DATE);
        CREATE TABLE payment_methods(payment_method_id INT PRIMARY KEY, name VARCHAR(100), commission DECIMAL(18,2), active BIT);
        CREATE TABLE payments(payment_id INT IDENTITY PRIMARY KEY, client_id INT, payment_method_id INT, payment_date DATETIME, amount DECIMAL(18,2));
        CREATE TABLE rental_amount_history(rental_amount_history_id INT IDENTITY PRIMARY KEY, rental_id INT, amount DECIMAL(18,2), start_date DATETIME, end_date DATETIME);
        CREATE TABLE account_movements(movement_id INT IDENTITY PRIMARY KEY, rental_id INT, movement_date DATETIME, movement_type VARCHAR(10), concept VARCHAR(255), amount DECIMAL(18,2), payment_id INT NULL);
        CREATE TABLE client_month_balances(id INT IDENTITY PRIMARY KEY, rental_id INT, month_year VARCHAR(7), previous_balance DECIMAL(18,2), interests DECIMAL(18,2), monthly_debits DECIMAL(18,2), balance DECIMAL(18,2), paid DECIMAL(18,2), advanced_payment DECIMAL(18,2));
        CREATE TABLE lockers(locker_id INT PRIMARY KEY, identifier VARCHAR(30), rental_id INT, warehouse_id INT, locker_type_id INT, active BIT);
        CREATE TABLE rental_lockers(rental_id INT, locker_id INT);
        CREATE TABLE emails(email_id INT IDENTITY PRIMARY KEY, client_id INT, address VARCHAR(255), active BIT);
        ALTER TABLE clients ADD color VARCHAR(30), receive_communications BIT, comment VARCHAR(500), comment_updated_at DATETIME;
        CREATE TABLE addresses(address_id INT IDENTITY PRIMARY KEY, client_id INT, street VARCHAR(200), city VARCHAR(100), province VARCHAR(100));
        CREATE TABLE billing_types(billing_type_id INT PRIMARY KEY, name VARCHAR(100));
        INSERT clients(client_id, full_name, payment_identifier, preferred_payment_method_id, initial_amount, active, increase_frequency_months) VALUES(1, 'Ariadna prueba', 5.13, 1, 232000, 1, 3);
        INSERT rentals VALUES(1, 1, '2020-01-01', NULL, 1, 2, 1, NULL, 1, '2026-10-01', 0, NULL, NULL);
        INSERT payment_methods VALUES(1, 'MP', 0, 1);
        INSERT rental_amount_history VALUES(1, 232000, '2020-01-01', NULL);
        """);
    var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migration.sql"));
    await db.ExecuteCommandAsync(migration);
    await db.ExecuteCommandAsync(migration);
    Check(true, "migration applies twice");
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
    async Task Pay(decimal amount, int day, bool skipFuture = true, string? action = null) =>
        await payments.CreatePaymentWithMovementAsync(new CreatePaymentTransaction
        {
            ClientId = 1, PaymentMethodId = 1, Amount = amount, Date = new DateTime(2026, 9, day),
            SkipFutureProjection = skipFuture, SurchargeAction = action,
            ExpectedPaymentStateToken = (await stateService.GetSnapshotAsync(1)).Token
        });
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
    await Pay(1,16, action:"next_payment");
    Check(await Scalar("SELECT amount FROM account_movements WHERE concept='Interés por mora de Septiembre 2026'") == 28000,
        "late fee retains every unpaid interest until prior principal is cleared");

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
        INSERT rentals(rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active,
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
