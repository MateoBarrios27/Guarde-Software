using System.Globalization;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Services.payment;
using GuardeSoftwareAPI.Services.sync;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PaymentCollectionCurrentMonthChecks
{
    private const int ClientId = 201;
    private const int RentalId = 201;

    public static async Task RunAsync(AccessDB db, PaymentService payments, ClientMonthBalanceService balances,
        PaymentStateService state, Action<bool, string> check)
    {
        CheckPlanner(check);
        var localNow = DateTime.UtcNow.AddHours(-3);
        var current = new DateTime(localNow.Year, localNow.Month, 1);
        var prior = current.AddMonths(-1);
        var following = current.AddMonths(1);
        await db.ExecuteCommandAsync("""
            INSERT clients(client_id,full_name,payment_identifier,preferred_payment_method_id,initial_amount,active,increase_frequency_months)
            VALUES(201,'Fecha cobranza actual prueba',2.57,1,281000,1,1200);
            SET IDENTITY_INSERT rentals ON;
            INSERT rentals(rental_id,client_id,start_date,active,months_unpaid,pending_surcharge)
            VALUES(201,201,'2000-01-01',1,1,0);
            SET IDENTITY_INSERT rentals OFF;
            INSERT rental_amount_history(rental_id,amount,start_date) VALUES(201,281000,'2000-01-01');
            """);
        async Task<decimal> Scalar(string sql) => Convert.ToDecimal(await db.ExecuteScalarAsync(sql));
        async Task Reset()
        {
            await db.ExecuteCommandAsync("""
                DELETE account_movements WHERE rental_id=201;
                DELETE payments WHERE client_id=201;
                DELETE client_month_balances WHERE rental_id=201;
                UPDATE rentals SET price_lock_end_date=NULL,pending_surcharge=0,pending_surcharge_period=NULL,
                    pending_surcharge_rent_base=NULL,increase_anchor_date=NULL WHERE rental_id=201;
                """);
            foreach (var movement in Ariel(current, false))
                await db.ExecuteCommandAsync("""
                    INSERT account_movements(rental_id,movement_date,movement_type,concept,amount)
                    VALUES(201,@date,@type,@concept,@amount)
                    """, [new SqlParameter("@date", movement.MovementDate), new SqlParameter("@type", movement.MovementType),
                    new SqlParameter("@concept", movement.Concept), new SqlParameter("@amount", movement.Amount)]);
            await balances.RebuildForRentalAsync(RentalId);
        }
        async Task<CreatePaymentTransaction> Dto(decimal amount) => new()
        {
            ClientId=ClientId, PaymentMethodId=1, Amount=amount, Date=current.AddDays(14),
            ExpectedPaymentStateToken=(await state.GetSnapshotAsync(ClientId)).Token
        };
        async Task<PaymentDecisionDetails> Consult(CreatePaymentTransaction dto, int expectedMovements = 6)
        {
            var token = dto.ExpectedPaymentStateToken;
            try { await payments.CreatePaymentWithMovementAsync(dto); }
            catch (PaymentDecisionRequiredException exception)
            {
                check(exception.Details.Scenario=="partial_payment", "old partial debt requires collection-month consultation");
                check(await Scalar("SELECT COUNT(*) FROM payments WHERE client_id=201")==0
                    && await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=201")==expectedMovements,
                    "old-debt consultation rolls back payment and every proposed debit");
                check((await state.GetSnapshotAsync(ClientId)).Token==token,
                    "old-debt consultation keeps the original financial state token");
                return exception.Details;
            }
            throw new Exception("Old partial debt was committed without a collection-month consultation.");
        }
        async Task VerifyReaders(DateTime due, decimal previousBalance, decimal balance, string context)
        {
            var dao = new DaoClient(db);
            var list = (await dao.GetClients()).Select("client_id=201").Single();
            check(Convert.ToDateTime(list["next_payment_day"])==due
                && Convert.ToDecimal(list["PreviousBalance"])==previousBalance && Convert.ToDecimal(list["balance"])==balance,
                context+" client list uses visible month and preserves both balances");
            var detail = (await dao.GetClientDetailByIdAsync(ClientId)).Rows[0];
            check(Convert.ToDateTime(detail["next_payment_day"])==due && Convert.ToDecimal(detail["balance"])==balance,
                context+" client detail uses visible month and full outstanding debt");
            var filtered = (await dao.GetTableClientsAsync(new GetClientsRequestDto { SearchTerm="Fecha cobranza actual prueba" })).clients.Single();
            check(filtered.NextPaymentDay==due && filtered.PreviousBalance==previousBalance && filtered.Balance==balance,
                context+" paged Finance/Clients data matches preview");
            var cached = (await new SyncService(db,payments,NullLogger<SyncService>.Instance).GetSnapshotAsync()).Clients.Single(c=>c.Id==ClientId);
            check(DateTime.Parse(cached.NextPaymentDay!, CultureInfo.InvariantCulture)==due
                && cached.PreviousBalance==previousBalance && cached.Balance==balance,
                context+" offline snapshot uses visible month and preserves debt");
            var dashboard = (await new DaoRental(db).GetPendingPaymentsAsync()).Select("client_id=201").Single();
            check(Convert.ToDateTime(dashboard["NextPaymentDay"])==due
                && Convert.ToDecimal(dashboard["PreviousBalance"])==previousBalance,
                context+" Dashboard uses visible month and previous principal");
            var communications = new CommunicationDao(db);
            check((await communications.GetClientsForSelectorAsync()).Single(c=>c.Id==ClientId).NextPaymentDate==due,
                context+" communications selector uses visible month");
            var statement = await communications.GetClientFinancialData(ClientId);
            check(statement.PreviousBalance==-previousBalance && statement.CurrentBalance==-balance+2.57m
                && statement.Surcharge==89800m,
                context+" account statement retains old principal and full debt");
            await db.ExecuteCommandAsync("UPDATE clients SET active=0 WHERE client_id=201");
            var others = await new CashDao(db).GetPendingCollectionAsync(due.Month,due.Year);
            await db.ExecuteCommandAsync("UPDATE clients SET active=1 WHERE client_id=201");
            var including = await new CashDao(db).GetPendingCollectionAsync(due.Month,due.Year);
            check(including-others==-balance, context+" Caja collects the entire debt in the visible month");
        }

        foreach (var action in new[] { "keep_month_pending", "close_partial_month" })
        {
            await Reset();
            var dto = await Dto(400000);
            var decision = await Consult(dto);
            var option = decision.Options.Single(o=>o.Action==action);
            var close = action=="close_partial_month";
            check(option.NextPaymentDate==(close ? following : current)
                && option.PreviousBalance==(close ? -443000 : -162000)
                && option.ResultingBalance==(close ? -813800 : -532800),
                "Ariel preview: current/following month has correct principal and total balance");
            check(option.ArrearsMonth==prior && option.Debits.Count==(close ? 1 : 0),
                "both choices preserve September-equivalent arrears and only closing issues one following rent");
            dto.FutureDebitAction=action;
            dto.PaymentDecisionToken=decision.DecisionToken;
            dto.ExpectedPaymentStateToken=decision.ExpectedPaymentStateToken;
            await payments.CreatePaymentWithMovementAsync(dto);
            await VerifyReaders(option.NextPaymentDate,option.PreviousBalance,option.ResultingBalance,action);
            check(Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentArrearsMonth(201)"))==prior
                && await Scalar($"SELECT dbo.GetRentalUnpaidMonths(201,'{current:yyyy-MM-dd}')")==1,
                "choosing a newer collection month does not erase prior-month arrears");
            await balances.RebuildForRentalAsync(RentalId);
            await VerifyReaders(option.NextPaymentDate,option.PreviousBalance,option.ResultingBalance,action+" rebuilt");

            var next = await Dto(10000);
            next.Date=next.Date.AddMinutes(1);
            await payments.CreatePaymentWithMovementAsync(next);
            check(Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentCollectionMonth(201)"))==option.NextPaymentDate
                && Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentArrearsMonth(201)"))==prior,
                "another payment against old debt keeps the deliberate collection month and original arrears");
            var oldPrincipal = await Dto(152000);
            oldPrincipal.Date=oldPrincipal.Date.AddMinutes(2);
            await payments.CreatePaymentWithMovementAsync(oldPrincipal);
            check(await Scalar($"SELECT dbo.GetRentalUnpaidMonths(201,'{current:yyyy-MM-dd}')")==0,
                "settling all old principal releases the old arrears anchor");
        }

        // The current collection month can be a consultation target even when
        // its rental debit has not been issued. Keeping it must not create one.
        await Reset();
        await db.ExecuteCommandAsync("""
            DELETE account_movements WHERE rental_id=201 AND movement_type='DEBITO'
                AND concept LIKE 'Alquiler %' AND movement_date=@current
            """, [new SqlParameter("@current",current)]);
        await balances.RebuildForRentalAsync(RentalId);
        var absentCurrentDto = await Dto(400000);
        var absentCurrentDecision = await Consult(absentCurrentDto,5);
        var absentCurrentOption = absentCurrentDecision.Options.Single(o=>o.Action=="keep_month_pending");
        check(absentCurrentOption.NextPaymentDate==current && absentCurrentOption.Debits.Count==0
            && absentCurrentOption.PreviousBalance==-162000 && absentCurrentOption.ResultingBalance==-251800,
            "missing current rental debit still permits keeping current collection month without issuance");
        absentCurrentDto.FutureDebitAction="keep_month_pending";
        absentCurrentDto.PaymentDecisionToken=absentCurrentDecision.DecisionToken;
        absentCurrentDto.ExpectedPaymentStateToken=absentCurrentDecision.ExpectedPaymentStateToken;
        await payments.CreatePaymentWithMovementAsync(absentCurrentDto);
        check(Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentCollectionMonth(201)"))==current
            && Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentArrearsMonth(201)"))==prior,
            "missing current debit leaves collection decision valid through its outstanding old principal");
        await VerifyReaders(current,-162000,-251800,"missing current rental debit");
        var heldAdvanceDto=await Dto(10000);
        heldAdvanceDto.Date=heldAdvanceDto.Date.AddMinutes(1);
        heldAdvanceDto.IsAdvancePayment=true;
        heldAdvanceDto.AdvanceMonths=2;
        PaymentDecisionDetails heldAdvanceDecision;
        try
        {
            await payments.CreatePaymentWithMovementAsync(heldAdvanceDto);
            throw new Exception("Short advance unexpectedly committed without consultation.");
        }
        catch (PaymentDecisionRequiredException exception)
        {
            heldAdvanceDecision=exception.Details;
        }
        var heldWithoutProjection=heldAdvanceDecision.Options.Single(o=>o.Action=="no_projection");
        check(heldAdvanceDecision.Scenario=="advance_shortfall"
            && heldWithoutProjection.NextPaymentDate==current && heldWithoutProjection.PreviousBalance==-152000
            && heldWithoutProjection.ResultingBalance==-241800 && heldWithoutProjection.Debits.Count==0,
            "short advance without a current debit keeps the held collection date and previews the remaining old principal");
        check(await Scalar("SELECT COUNT(*) FROM payments WHERE client_id=201")==1
            && (await state.GetSnapshotAsync(ClientId)).Token==heldAdvanceDto.ExpectedPaymentStateToken,
            "short-advance consultation leaves the original confirmed payment and state intact");
        heldAdvanceDto.FutureDebitAction="no_projection";
        heldAdvanceDto.PaymentDecisionToken=heldAdvanceDecision.DecisionToken;
        heldAdvanceDto.ExpectedPaymentStateToken=heldAdvanceDecision.ExpectedPaymentStateToken;
        await payments.CreatePaymentWithMovementAsync(heldAdvanceDto);
        await VerifyReaders(current,-152000,-241800,"short advance keeping current month without debit");

        await Reset();
        var deletionDto = await Dto(400000);
        var deletionDecision = await Consult(deletionDto);
        deletionDto.FutureDebitAction="close_partial_month";
        deletionDto.PaymentDecisionToken=deletionDecision.DecisionToken;
        deletionDto.ExpectedPaymentStateToken=deletionDecision.ExpectedPaymentStateToken;
        await payments.CreatePaymentWithMovementAsync(deletionDto);
        await db.ExecuteCommandAsync("""
            DELETE account_movements WHERE rental_id=201 AND movement_type='DEBITO'
                AND concept LIKE 'Alquiler %' AND movement_date=@prior
            """, [new SqlParameter("@prior",prior)]);
        await balances.RebuildForRentalAsync(RentalId);
        check(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentArrearsMonth(201)") is DBNull
            && await Scalar($"SELECT dbo.GetRentalUnpaidMonths(201,'{current:yyyy-MM-dd}')")==0,
            "deleting the old rent debit invalidates its arrears anchor instead of leaving ghost delinquency");

        // Simulate an existing pre-migration decision. Reading or re-running migration
        // must correct its visible month without rewriting that decision or the ledger.
        await Reset();
        await db.ExecuteCommandAsync("""
            INSERT payments(client_id,payment_method_id,payment_date,amount) VALUES(201,1,@date,400000);
            DECLARE @PaymentId INT = SCOPE_IDENTITY();
            INSERT account_movements(rental_id,movement_date,movement_type,concept,amount,payment_id)
            VALUES(201,@date,'CREDITO','Pago histórico parcial',400000,@PaymentId);
            INSERT payment_collection_decisions(payment_id,rental_id,next_payment_month,action,ledger_anchor_id)
            SELECT @PaymentId,201,@oldMonth,'keep_month_pending',MAX(movement_id)
            FROM account_movements WHERE rental_id=201;
            """, [new SqlParameter("@date",current.AddDays(14)),new SqlParameter("@oldMonth",prior)]);
        await balances.RebuildForRentalAsync(RentalId);
        var beforeLedger = await db.ExecuteScalarAsync("SELECT STRING_AGG(CONCAT(movement_id,':',movement_type,':',amount,':',CONVERT(varchar(30),movement_date,126)), '|') WITHIN GROUP (ORDER BY movement_id) FROM account_movements WHERE rental_id=201");
        var beforePayments = await Scalar("SELECT SUM(amount) FROM payments WHERE client_id=201");
        // Exercise the upgrade from a genuinely old schema, not only CREATE OR
        // ALTER on an installation where the baseline already includes the column.
        // These functions are not schema-bound; do not call any consumer until the
        // migration has restored their referenced column.
        await db.ExecuteCommandAsync("""
            IF EXISTS (SELECT 1 FROM sys.check_constraints
                WHERE parent_object_id=OBJECT_ID('dbo.payment_collection_decisions')
                    AND name='CK_payment_collection_decisions_arrears_month')
                ALTER TABLE dbo.payment_collection_decisions DROP CONSTRAINT CK_payment_collection_decisions_arrears_month;
            ALTER TABLE dbo.payment_collection_decisions DROP COLUMN arrears_month;
            """);
        check(await Scalar("SELECT CASE WHEN COL_LENGTH('dbo.payment_collection_decisions','arrears_month') IS NULL THEN 1 ELSE 0 END")==1,
            "upgrade fixture genuinely starts without the arrears anchor column");
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"collection-current-month-migration.sql"));
        for (var pass=0;pass<2;pass++)
            foreach (var batch in System.Text.RegularExpressions.Regex.Split(migration,@"(?im)^GO\s*$"))
                if (!string.IsNullOrWhiteSpace(batch)) await db.ExecuteCommandAsync(batch);
        check(await Scalar("SELECT CASE WHEN COL_LENGTH('dbo.payment_collection_decisions','arrears_month') IS NOT NULL THEN 1 ELSE 0 END")==1,
            "migration adds the missing arrears anchor column and remains repeatable");
        check(Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentCollectionMonth(201)"))==prior
            && Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentCollectionDisplayMonth(201)"))==current
            && Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentArrearsMonth(201)"))==prior,
            "existing raw past decision remains intact while visible month advances and arrears stay historical");
        await VerifyReaders(current,-162000,-532800,"existing past decision");
        var afterLedger = await db.ExecuteScalarAsync("SELECT STRING_AGG(CONCAT(movement_id,':',movement_type,':',amount,':',CONVERT(varchar(30),movement_date,126)), '|') WITHIN GROUP (ORDER BY movement_id) FROM account_movements WHERE rental_id=201");
        check(Equals(beforeLedger,afterLedger) && await Scalar("SELECT SUM(amount) FROM payments WHERE client_id=201")==beforePayments,
            "migration and all readers leave historic payments, movement dates and amounts unchanged");
        check(await Scalar($"SELECT dbo.GetRentalUnpaidMonths(201,'{current:yyyy-MM-dd}')")==1,
            "existing past decision retains the same prior-month arrears classification");
        var tokenBefore = (await state.GetSnapshotAsync(ClientId)).Token;
        await db.ExecuteCommandAsync("UPDATE payment_collection_decisions SET arrears_month=DATEADD(month,-1,next_payment_month) WHERE rental_id=201");
        check((await state.GetSnapshotAsync(ClientId)).Token!=tokenBefore,
            "financial state token includes the persisted arrears anchor");
        await db.ExecuteCommandAsync("UPDATE clients SET active=0 WHERE client_id=201");
    }

    private static void CheckPlanner(Action<bool,string> check)
    {
        foreach (var current in new[] { new DateTime(2026,10,1), new DateTime(2027,1,1) })
        {
            var ledger = Ariel(current,true);
            var before = PaymentAllocationEngine.Allocate(ledger.Where(m=>m.MovementType!="CREDITO"));
            var after = PaymentAllocationEngine.Allocate(ledger);
            var dto = new CreatePaymentTransaction { ClientId=ClientId,PaymentMethodId=1,Amount=400000,Date=current.AddDays(14) };
            List<RentalAmountHistory> history = [new() { RentalId=RentalId,Amount=281000,StartDate=new DateTime(2000,1,1) }];
            PaymentProjectionPlan Plan(DateTime floor, List<AccountMovement>? movements=null) => PaymentProjectionPlanner.Plan(dto,before,
                movements==null ? after : PaymentAllocationEngine.Allocate(movements),7,history,281000,false,false,"fixed-state",532800,
                movements ?? ledger,null,floor);
            var plan=Plan(current);
            check(plan.Decision is { Scenario:"partial_payment",Options.Count:2 },
                "planner consults current versus following month when payment only partially reaches old rent");
            var keep=plan.Decision!.Options.Single(o=>o.Action=="keep_month_pending");
            var close=plan.Decision.Options.Single(o=>o.Action=="close_partial_month");
            check(keep.NextPaymentDate==current && keep.Debits.Count==0 && keep.PreviousBalance==-162000 && keep.ResultingBalance==-532800,
                "deterministic Ariel current-month preview is exact, including January rollover");
            check(close.NextPaymentDate==current.AddMonths(1) && close.Debits.Count==1
                && close.Debits[0].Month==current.AddMonths(1) && close.PreviousBalance==-443000 && close.ResultingBalance==-813800,
                "deterministic Ariel following-month preview creates exactly one rent and preserves all debt");
            check(keep.ArrearsMonth==current.AddMonths(-1) && close.ArrearsMonth==current.AddMonths(-1)
                && after.Rows.Single(r=>r.MonthYear==current.AddMonths(-1).ToString("MM/yyyy")).UnpaidRent==162000,
                "collection choices leave old principal allocations and arrears unchanged");
            var existing=ledger.Concat([new AccountMovement { Id=8,RentalId=RentalId,MovementDate=current.AddMonths(1),
                MovementType="DEBITO",Concept=RentConcept(current.AddMonths(1)),Amount=281000 }]).ToList();
            check(Plan(current,existing).Decision!.Options.Single(o=>o.Action=="close_partial_month").Debits.Count==0,
                "an existing following-month debit is reused instead of duplicated");
            var nextMonthPlan=Plan(current.AddMonths(1));
            check(nextMonthPlan.Decision!.Options.All(o=>o.NextPaymentDate>=current.AddMonths(1))
                && nextMonthPlan.Decision.DecisionToken!=plan.Decision.DecisionToken,
                "month rollover refreshes every option and invalidates the previous decision approval");
            var rolloverBefore=PaymentAllocationEngine.Allocate(existing);
            var rolloverDate=current.AddMonths(1);
            var rolloverDto=new CreatePaymentTransaction {
                ClientId=ClientId,PaymentMethodId=1,Amount=162000,Date=rolloverDate.AddDays(14) };
            var rolloverLedger=existing.Concat([new AccountMovement {
                Id=9,RentalId=RentalId,MovementDate=rolloverDto.Date,MovementType="CREDITO",
                Concept="Cancela capital viejo",Amount=162000,PaymentId=9 }]).ToList();
            var rolloverAfter=PaymentAllocationEngine.Allocate(rolloverLedger);
            var rollover=PaymentProjectionPlanner.Plan(rolloverDto,rolloverBefore,rolloverAfter,9,history,281000,
                false,false,"rollover-state",651800,rolloverLedger,rolloverDate,rolloverDate,current.AddMonths(-1));
            check(rollover.Decision==null && rollover.Debits.Count==0 && rollover.CollectionMonth==rolloverDate
                && rollover.ArrearsMonth==current,
                "settling previous arrears after month rollover advances anchor to the next old unpaid month without reconsultation");

            var futureMonth=current.AddMonths(1);
            List<AccountMovement> futureLedger=[
                new() { Id=1,RentalId=RentalId,MovementDate=futureMonth,MovementType="DEBITO",
                    Concept=RentConcept(futureMonth),Amount=281000 },
                new() { Id=2,RentalId=RentalId,MovementDate=current.AddDays(14),MovementType="CREDITO",
                    Concept="Pago parcial adelantado",Amount=100000,PaymentId=2 }
            ];
            var futureDto=new CreatePaymentTransaction {
                ClientId=ClientId,PaymentMethodId=1,Amount=100000,Date=current.AddDays(14) };
            var futurePartial=PaymentProjectionPlanner.Plan(futureDto,
                PaymentAllocationEngine.Allocate(futureLedger.Where(m=>m.Id!=2)),PaymentAllocationEngine.Allocate(futureLedger),
                2,history,281000,false,false,"future-state",181000,futureLedger,null,current);
            check(futurePartial.Decision!.Options.Single(o=>o.Action=="keep_month_pending").NextPaymentDate==futureMonth
                && futurePartial.Decision!.Options.Single(o=>o.Action=="close_partial_month").NextPaymentDate==futureMonth.AddMonths(1),
                "partially paid future month is preserved and never pulled back to the current month");

            List<AccountMovement> firstAdvanceLedger=[
                new() { Id=1,RentalId=RentalId,MovementDate=current.AddMonths(-1),MovementType="DEBITO",
                    Concept=RentConcept(current.AddMonths(-1)),Amount=100 },
                new() { Id=2,RentalId=RentalId,MovementDate=current.AddDays(14),MovementType="CREDITO",
                    Concept="Primer adelanto parcial",Amount=60,PaymentId=2 }
            ];
            var firstAdvanceDto=new CreatePaymentTransaction {
                ClientId=ClientId,PaymentMethodId=1,Amount=60,Date=current.AddDays(14),IsAdvancePayment=true,AdvanceMonths=2 };
            var firstAdvance=PaymentProjectionPlanner.Plan(firstAdvanceDto,
                PaymentAllocationEngine.Allocate(firstAdvanceLedger.Where(m=>m.Id!=2)),PaymentAllocationEngine.Allocate(firstAdvanceLedger),
                2,[new RentalAmountHistory { RentalId=RentalId,Amount=100,StartDate=new DateTime(2000,1,1) }],
                100,false,false,"first-advance-state",40,firstAdvanceLedger,null,current);
            var firstAdvanceNoProjection=firstAdvance.Decision!.Options.Single(o=>o.Action=="no_projection");
            var firstAdvanceSelected=firstAdvance.Decision.Options.Single(o=>o.Action=="selected_months");
            check(firstAdvance.Decision.Scenario=="advance_shortfall"
                && firstAdvanceNoProjection.NextPaymentDate==current && firstAdvanceNoProjection.PreviousBalance==-40
                && firstAdvanceNoProjection.ResultingBalance==-40 && firstAdvanceNoProjection.Debits.Count==0
                && firstAdvanceSelected.NextPaymentDate==current && firstAdvanceSelected.PreviousBalance==-40
                && firstAdvanceSelected.ResultingBalance==-140 && firstAdvanceSelected.Debits.Count==1,
                "first short advance without an existing choice exposes old principal consistently for both projection options");

            var absentCurrent=ledger.Where(m=>m.MovementType!="DEBITO" || m.MovementDate!=current || !(m.Concept?.StartsWith("Alquiler ") ?? false)).ToList();
            var missingCurrent=Plan(current,absentCurrent).Decision!.Options.Single(o=>o.Action=="keep_month_pending");
            check(missingCurrent.NextPaymentDate==current && missingCurrent.Debits.Count==0
                && missingCurrent.ArrearsMonth==current.AddMonths(-1) && missingCurrent.ResultingBalance==-251800,
                "planner keeps a current collection month with no current rent debit while retaining old debt");

            dto.Date=current.AddMonths(-1).AddDays(14);
            var backdatedLedger=ledger.Select(m=>m.MovementType=="CREDITO" ? new AccountMovement {
                Id=m.Id,RentalId=m.RentalId,MovementDate=dto.Date,MovementType=m.MovementType,
                Concept=m.Concept,Amount=m.Amount,PaymentId=m.PaymentId } : m).ToList();
            var backdated=Plan(current,backdatedLedger);
            check(backdated.Decision!.Options.All(o=>o.NextPaymentDate>=current && o.Debits.All(d=>d.Month>=current)),
                "backdated payment respects current Argentina collection-month floor");
        }
    }

    private static List<AccountMovement> Ariel(DateTime current, bool payment)
    {
        List<AccountMovement> ledger=[];
        void Add(DateTime month,string concept,decimal amount) => ledger.Add(new AccountMovement {
            Id=ledger.Count+1,RentalId=RentalId,MovementDate=month,MovementType="DEBITO",Concept=concept,Amount=amount });
        Add(current.AddMonths(-2),RentConcept(current.AddMonths(-2)),281000);
        Add(current.AddMonths(-1),RentConcept(current.AddMonths(-1)),281000);
        Add(current,RentConcept(current),281000);
        Add(current.AddMonths(-2),"Interés por mora",25500);
        Add(current.AddMonths(-1),"Interés por mora",30600);
        Add(current,"Interés por mora",33700);
        if (payment) ledger.Add(new AccountMovement { Id=7,RentalId=RentalId,MovementDate=current.AddDays(14),
            MovementType="CREDITO",Concept="Pago",Amount=400000,PaymentId=7 });
        return ledger;
    }

    private static string RentConcept(DateTime month) =>
        $"Alquiler {CultureInfo.GetCultureInfo("es-AR").DateTimeFormat.GetMonthName(month.Month)} {month.Year}";
}
