using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Controllers;
using GuardeSoftwareAPI.Dtos.Sync;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Services.payment;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Services.sync;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PaymentDecisionChecks
{
    public static async Task RunAsync(AccessDB db, PaymentService payments, ClientMonthBalanceService balances,
        PaymentStateService state, Action<bool, string> check)
    {
        await db.ExecuteCommandAsync("""
            INSERT clients(client_id,full_name,payment_identifier,preferred_payment_method_id,initial_amount,active,increase_frequency_months)
            VALUES(100,'Excedente prueba',0,1,100,1,4);
            SET IDENTITY_INSERT rentals ON;
            INSERT rentals(rental_id,client_id,start_date,active,months_unpaid,pending_surcharge)
            VALUES(100,100,'2026-01-01',1,0,0);
            SET IDENTITY_INSERT rentals OFF;
            """);
        async Task<decimal> Scalar(string sql) => Convert.ToDecimal(await db.ExecuteScalarAsync(sql));
        async Task Reset(bool rent = true)
        {
            await db.ExecuteCommandAsync("""
                DELETE account_movements WHERE rental_id=100;
                DELETE client_month_balances WHERE rental_id=100;
                DELETE payments WHERE client_id=100;
                DELETE rental_amount_history WHERE rental_id=100;
                INSERT rental_amount_history(rental_id,amount,start_date) VALUES(100,100,'2026-01-01');
                UPDATE clients SET departure_status=NULL WHERE client_id=100;
                UPDATE rentals SET price_lock_end_date=NULL,pending_surcharge=0,pending_surcharge_period=NULL,
                    pending_surcharge_rent_base=NULL,increase_anchor_date=NULL WHERE rental_id=100;
                """);
            if (rent) await db.ExecuteCommandAsync("""
                INSERT account_movements(rental_id,movement_date,movement_type,concept,amount)
                VALUES(100,'2026-10-01','DEBITO','Alquiler Octubre 2026',100);
                """);
            await balances.RebuildForRentalAsync(100);
        }
        async Task<CreatePaymentTransaction> Dto(decimal amount) => new()
        {
            ClientId=100,PaymentMethodId=1,Amount=amount,Date=new DateTime(2026,10,5),
            ExpectedPaymentStateToken=(await state.GetSnapshotAsync(100)).Token
        };
        async Task<PaymentDecisionDetails> Consult(CreatePaymentTransaction dto, string scenario)
        {
            try { await payments.CreatePaymentWithMovementAsync(dto); }
            catch (PaymentDecisionRequiredException ex)
            {
                check(ex.Details.Scenario==scenario,$"consultation for {scenario}");
                return ex.Details;
            }
            throw new Exception("Payment unexpectedly committed without consultation: "+scenario);
        }
        async Task Confirm(CreatePaymentTransaction dto, PaymentDecisionDetails decision, string action)
        {
            dto.FutureDebitAction=action; dto.PaymentDecisionToken=decision.DecisionToken;
            dto.ExpectedPaymentStateToken=decision.ExpectedPaymentStateToken;
            await payments.CreatePaymentWithMovementAsync(dto);
        }
        async Task<decimal> Rents() => await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=100 AND concept LIKE 'Alquiler%'");
        async Task<decimal> Net() => await Scalar("SELECT SUM(CASE WHEN movement_type='CREDITO' THEN amount ELSE -amount END) FROM account_movements WHERE rental_id=100");
        async Task CheckVisibleCredit(decimal amount, decimal? projectedCredit = null)
        {
            var clients=await new DaoClient(db).GetClients();
            var row=clients.Select("client_id=100").Single();
            check(Convert.ToDecimal(row["PreviousBalance"])==amount && Convert.ToDecimal(row["balance"])==amount,
                "client list exposes actual unallocated credit");
            var detail=await new DaoClient(db).GetClientDetailByIdAsync(100);
            check(Convert.ToDecimal(detail.Rows[0]["balance"])==amount,"client detail exposes actual credit");
            var snapshot=await new SyncService(db,payments,NullLogger<SyncService>.Instance).GetSnapshotAsync();
            var cached=snapshot.Clients.Single(client=>client.Id==100);
            check(cached.PreviousBalance==amount && cached.Balance==amount,"offline snapshot exposes actual credit");
            var statement=await new CommunicationDao(db).GetClientFinancialData(100);
            check(statement.PreviousBalance==-amount && statement.CurrentBalance==0,"account statement preserves credit without demanding a payment");
            var projected=await new CommunicationDao(db).GetClientFinancialData(100,true);
            check(projected.PreviousBalance==-(projectedCredit ?? amount) && projected.CurrentBalance==Math.Max(0m,100m-(projectedCredit ?? amount)),
                "projected account statement subtracts real credit from next rent");
        }

        async Task VerifyOutcome(PaymentDecisionOption option, string context)
        {
            var clients = await new DaoClient(db).GetClients();
            var row = clients.Select("client_id=100").Single();
            check(Convert.ToDateTime(row["next_payment_day"]) == option.NextPaymentDate, context+" list collection date matches preview");
            check(Convert.ToDecimal(row["balance"]) == option.ResultingBalance
                && Convert.ToDecimal(row["PreviousBalance"]) == option.PreviousBalance, context+" actual balance and previous balance match preview");
            var detail = (await new DaoClient(db).GetClientDetailByIdAsync(100)).Rows[0];
            check(Convert.ToDateTime(detail["next_payment_day"]) == option.NextPaymentDate, context+" detail date matches preview");
            var page = await new DaoClient(db).GetTableClientsAsync(new GetClientsRequestDto { SearchTerm="Excedente prueba" });
            check(page.clients.Single(c=>c.Id==100).NextPaymentDay == option.NextPaymentDate, context+" filtered client date matches preview");
            var cached = (await new SyncService(db,payments,NullLogger<SyncService>.Instance).GetSnapshotAsync()).Clients.Single(c=>c.Id==100);
            check(DateTime.Parse(cached.NextPaymentDay!) == option.NextPaymentDate
                && cached.Balance == option.ResultingBalance && cached.PreviousBalance == option.PreviousBalance,
                context+" offline date and balances match preview");
            var statement = await new CommunicationDao(db).GetClientFinancialData(100);
            check(statement.PreviousBalance == -option.PreviousBalance
                && statement.CurrentBalance == Math.Max(0,-option.ResultingBalance), context+" account statement agrees with chosen month and full debt");
            var recipient = (await new CommunicationDao(db).GetClientsForSelectorAsync()).Single(c=>c.Id==100);
            check(recipient.NextPaymentDate == option.NextPaymentDate, context+" communications date matches preview");
            if (option.ResultingBalance < 0) {
                var pending = (await new DaoRental(db).GetPendingPaymentsAsync()).Select("client_id=100").Single();
                check(Convert.ToDateTime(pending["NextPaymentDay"]) == option.NextPaymentDate
                    && Convert.ToDecimal(pending["PreviousBalance"]) == option.PreviousBalance,
                    context+" dashboard date and previous balance match preview");
            }
            await db.ExecuteCommandAsync("UPDATE clients SET active=0 WHERE client_id=100");
            var otherCollections = await new CashDao(db).GetPendingCollectionAsync(option.NextPaymentDate.Month, option.NextPaymentDate.Year);
            await db.ExecuteCommandAsync("UPDATE clients SET active=1 WHERE client_id=100");
            var collections = await new CashDao(db).GetPendingCollectionAsync(option.NextPaymentDate.Month, option.NextPaymentDate.Year);
            check(collections-otherCollections == Math.Max(0,-option.ResultingBalance), context+" Caja includes debt in chosen collection month");
        }

        await Reset();
        var dto=await Dto(110);
        var initialToken=dto.ExpectedPaymentStateToken;
        var decision=await Consult(dto,"surplus");
        check(decision.Surplus==10 && decision.Options[0].Action=="credit_next_month" && decision.Options[0].ResultingBalance==-90,
            "small surplus recommends one rent and previews remaining 90");
        check(await Scalar("SELECT COUNT(*) FROM payments WHERE client_id=100")==0 && await Rents()==1,
            "consultation rolls back payment and every proposed debit");
        check((await state.GetSnapshotAsync(100)).Token==initialToken,"consultation preserves the original state token");

        var controller=new PaymentController(payments,null!,new DaoUser(db),NullLogger<PaymentController>.Instance)
        { ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext() } };
        var apiResult=await controller.CreatePaymentTransaction(dto);
        check(apiResult.Result is UnprocessableEntityObjectResult { StatusCode:422, Value:PaymentDecisionDetails { Code:"PAYMENT_DECISION_REQUIRED" } },
            "payment API returns a structured consultation instead of recording an excess payment");
        var syncResult=await new SyncService(db,payments,NullLogger<SyncService>.Instance).ProcessOfflinePaymentsAsync(new SyncPaymentsRequestDto
        {
            Payments=[new OfflinePaymentDto { LocalId="offline-decision-check",ClientId=100,PaymentMethodId=1,
                Amount=110,Date=dto.Date,ExpectedPaymentStateToken=initialToken,SkipFutureProjection=false }]
        });
        check(!syncResult.Results.Single().Success && await Scalar("SELECT COUNT(*) FROM payments WHERE client_id=100")==0 && await Rents()==1,
            "offline synchronization never resolves a financial decision silently or leaves a payment behind");
        await Confirm(dto,decision,"credit_next_month");
        check(await Rents()==2 && await Net()==-90,"small surplus creates exactly one next debit");
        var list=await new DaoClient(db).GetClients();
        check(Convert.ToDecimal(list.Select("client_id=100").Single()["PreviousBalance"])==10,
            "small surplus is shown as positive Saldo anterior");
        check(await Scalar("SELECT COUNT(*) FROM payments WHERE client_id=100")==1,"confirmation creates one payment only");
        await balances.RebuildForRentalAsync(100);
        check(await Net()==-90 && await Rents()==2,"confirmed small surplus survives rebuild");
        await VerifyOutcome(decision.Options.Single(o=>o.Action=="credit_next_month"),"surplus kept pending");

        await Reset(); dto=await Dto(110); decision=await Consult(dto,"surplus");
        await Confirm(dto,decision,"close_credited_month");
        check(await Rents()==3 && await Net()==-190,"closing credited month creates one further rent and preserves unpaid principal");
        await VerifyOutcome(decision.Options.Single(o=>o.Action=="close_credited_month"),"credited month closed");

        await Reset(); dto=await Dto(350); decision=await Consult(dto,"surplus");
        await Confirm(dto,decision,"credit_next_month");
        check(await Rents()==2 && await Net()==150,"large surplus also stops after one chosen debit");
        await CheckVisibleCredit(150,250);

        await Reset(); dto=await Dto(100); await payments.CreatePaymentWithMovementAsync(dto);
        check(await Rents()==2 && await Net()==-100,"ordinary exact payment projects only the next rent without consultation");

        await Reset(); dto=await Dto(50); decision=await Consult(dto,"partial_payment");
        check(decision.Options[0].Action=="keep_month_pending" && decision.Options[0].ResultingBalance==-50,
            "partial payment recommends keeping the existing debt");
        await Confirm(dto,decision,"keep_month_pending");
        check(await Rents()==1 && await Net()==-50,"partial payment does not silently debit another month");
        await VerifyOutcome(decision.Options.Single(o=>o.Action=="keep_month_pending"),"partial month kept pending");
        var partialSecond = await Dto(50);
        partialSecond.Date = partialSecond.Date.AddMinutes(1);
        await payments.CreatePaymentWithMovementAsync(partialSecond);
        check(await Rents()==2 && await Net()==-100,"settling a held partial month issues its following rent once");
        var secondCreditId = (int)await Scalar("SELECT MAX(movement_id) FROM account_movements WHERE rental_id=100 AND movement_type='CREDITO'");
        await payments.DeletePaymentAsync(secondCreditId);
        await VerifyOutcome(decision.Options.Single(o=>o.Action=="keep_month_pending"),"deleting the subsequent payment restores previous choice");
        await Reset(); dto=await Dto(50); decision=await Consult(dto,"partial_payment");
        await Confirm(dto,decision,"close_partial_month");
        check(await Rents()==2 && await Net()==-150,"explicit partial-payment choice keeps debt and issues one next rent");
        await VerifyOutcome(decision.Options.Single(o=>o.Action=="close_partial_month"),"partial month closed");
        var oldDebtOnly = await Dto(10);
        await payments.CreatePaymentWithMovementAsync(oldDebtOnly);
        check(Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentCollectionMonth(100)"))==new DateTime(2026,11,1),
            "payment against prior debt preserves already chosen following month without asking to close old month again");

        await Reset(); dto=await Dto(110); decision=await Consult(dto,"surplus");
        dto.Amount=111; dto.FutureDebitAction="credit_next_month"; dto.PaymentDecisionToken=decision.DecisionToken;
        var revised=await Consult(dto,"surplus");
        check(revised.DecisionToken!=decision.DecisionToken && await Scalar("SELECT COUNT(*) FROM payments WHERE client_id=100")==0,
            "changing the amount invalidates the prior decision");
        dto.FutureDebitAction="invalid_action"; dto.PaymentDecisionToken=revised.DecisionToken;
        await Consult(dto,"surplus");
        check(await Rents()==1,"unknown action cannot bypass consultation");
        await db.ExecuteCommandAsync("UPDATE rental_amount_history SET amount=120 WHERE rental_id=100");
        try { await Confirm(dto,revised,"credit_next_month"); throw new Exception("Stale state accepted"); }
        catch (PaymentConflictException) { check(true,"state change after consultation requires reloading before payment"); }

        await Reset(); await db.ExecuteCommandAsync("UPDATE clients SET departure_status='SE_VA' WHERE client_id=100");
        dto=await Dto(110); decision=await Consult(dto,"surplus");
        check(decision.Options.Count==1 && decision.Options[0].Debits.Count==0,"SE VA never offers future rents");
        await Confirm(dto,decision,"no_projection"); check(await Rents()==1 && await Net()==10,"departure surplus stays a credit");
        await Reset(); await db.ExecuteCommandAsync("UPDATE rentals SET price_lock_end_date='2027-05-01' WHERE rental_id=100");
        dto=await Dto(110); decision=await Consult(dto,"surplus");
        check(decision.Options.Count==1,"active price lock cannot be extended by surplus");
        await Confirm(dto,decision,"no_projection"); check(await Rents()==1,"price lock adds no extra rent");
        await Reset(); dto=await Dto(110); dto.SkipFutureProjection=true; decision=await Consult(dto,"surplus");
        check(decision.Options.Count==1,"skip projection is respected even with excess money");
        await Confirm(dto,decision,"no_projection"); check(await Net()==10,"skipped projection preserves credit");

        await Reset(); dto=await Dto(200); dto.IsAdvancePayment=true; dto.AdvanceMonths=2;
        await payments.CreatePaymentWithMovementAsync(dto);
        check(await Rents()==2 && await Net()==0,"exact two-month advance never adds a third month");
        await Reset(); dto=await Dto(210); dto.IsAdvancePayment=true; dto.AdvanceMonths=2;
        decision=await Consult(dto,"advance_surplus");
        check(decision.Surplus==10 && decision.Options[0].Debits.Count==1,"advance previews only surplus beyond selected months");
        await Confirm(dto,decision,"selected_months");
        check(await Rents()==2 && await Net()==10,"advance surplus leaves credit without extending selected period");
        await Reset(); dto=await Dto(150); dto.IsAdvancePayment=true; dto.AdvanceMonths=2;
        decision=await Consult(dto,"advance_shortfall");
        await Confirm(dto,decision,"selected_months");
        check(await Rents()==2 && await Net()==-50,"explicit short advance keeps selected periods with visible debt");
        await Reset(); dto=await Dto(150); dto.IsAdvancePayment=true; dto.AdvanceMonths=6;
        decision=await Consult(dto,"advance_shortfall");
        check(await Scalar("SELECT COUNT(*) FROM rentals WHERE rental_id=100 AND price_lock_end_date IS NOT NULL")==0,
            "unconfirmed advance does not leave a price freeze");
        await Confirm(dto,decision,"no_projection");
        check(await Rents()==1 && await Scalar("SELECT COUNT(*) FROM rentals WHERE rental_id=100 AND price_lock_end_date IS NOT NULL")==0,
            "recording payment only does not establish a new six-month freeze");
        await Reset(); dto=await Dto(600); dto.IsAdvancePayment=true; dto.AdvanceMonths=6;
        await payments.CreatePaymentWithMovementAsync(dto);
        check(await Rents()==6 && await Net()==0 && await Scalar("SELECT COUNT(*) FROM rentals WHERE rental_id=100 AND price_lock_end_date IS NOT NULL")==1,
            "exact six-month advance preserves freeze and has exactly six rents");

        await Reset(); await db.ExecuteCommandAsync("""
            INSERT account_movements(rental_id,movement_date,movement_type,concept,amount)
            VALUES(100,'2026-11-01','DEBITO','Alquiler Noviembre 2026 (Planificado)',100);
            """); await balances.RebuildForRentalAsync(100);
        dto=await Dto(110); decision=await Consult(dto,"partial_payment");
        await Confirm(dto,decision,"keep_month_pending");
        check(await Rents()==2 && await Net()==-90,"existing planned rent is retained without duplicate or automatic extension");

        await Reset(false); dto=await Dto(110); decision=await Consult(dto,"missing_rent");
        check(decision.Options.Single(o=>o.Action=="single_next").Debits[0].Month==new DateTime(2026,10,1),
            "missing rent offers payment month rather than skipping it");
        await Confirm(dto,decision,"single_next"); check(await Rents()==1 && await Net()==10,"first rent is created once with excess credit");


        await Reset(); await db.ExecuteCommandAsync("""
            INSERT account_movements(rental_id,movement_date,movement_type,concept,amount)
            VALUES(100,'2026-11-01','DEBITO','Interés por mora de Octubre 2026',20);
            """); await balances.RebuildForRentalAsync(100);
        dto=await Dto(120); await payments.CreatePaymentWithMovementAsync(dto);
        check(await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=100 AND concept='Alquiler Noviembre 2026'")==1
            && await Scalar("SELECT COUNT(*) FROM account_movements WHERE rental_id=100 AND concept='Alquiler Diciembre 2026'")==0,
            "future interest does not skip the next rental month");


        await Reset(); await db.ExecuteCommandAsync("""
            INSERT account_movements(rental_id,movement_date,movement_type,concept,amount)
            VALUES(100,'2026-11-01','DEBITO','Interés por mora de Octubre 2026',20);
            """); await balances.RebuildForRentalAsync(100);
        dto=await Dto(220); dto.IsAdvancePayment=true; dto.AdvanceMonths=2;
        await payments.CreatePaymentWithMovementAsync(dto);
        check(await Rents()==2 && await Net()==0,"future interest does not extend an exact selected advance by a month");
        await Reset(); dto=await Dto(100.01m); decision=await Consult(dto,"surplus");
        check(decision.Surplus==0.01m,"even one cent of excess requires a decision without an arbitrary tolerance");
        await Confirm(dto,decision,"credit_next_month");
        check(await Rents()==2 && await Net()==-99.99m,"cent-level excess creates only one next debit");

        await Reset(); dto=await Dto(121); dto.CommissionAmount=11;
        decision=await Consult(dto,"surplus");
        check(decision.Surplus==10,"commission is deducted exactly once in consultation");
        await Confirm(dto,decision,"credit_next_month"); check(await Net()==-90,"commission consultation agrees with the committed ledger");
        await Reset(); await db.ExecuteCommandAsync("UPDATE rentals SET pending_surcharge=10,pending_surcharge_period='2026-10-01',pending_surcharge_rent_base=100 WHERE rental_id=100");
        dto=await Dto(120); dto.SurchargeAction="immediate"; dto.SurchargeAmount=10;
        decision=await Consult(dto,"surplus");
        check(decision.Surplus==10 && decision.Options[0].ResultingBalance==-90,"immediate interest is reserved once in preview");
        await Confirm(dto,decision,"credit_next_month"); check(await Net()==-90,"immediate-interest result matches confirmed preview");
        await Reset(); await db.ExecuteCommandAsync("UPDATE rentals SET pending_surcharge=50,pending_surcharge_period='2026-10-01',pending_surcharge_rent_base=100 WHERE rental_id=100");
        dto=await Dto(120); dto.SurchargeAction="immediate"; dto.SurchargeAmount=10;
        decision=await Consult(dto,"partial_payment");
        check(decision.Surplus==0 && decision.Options[0].ResultingBalance==-30,
            "consultation uses the actual server fee when the entered fee differs");
        await Confirm(dto,decision,"keep_month_pending");
        check(await Net()==-30 && await Rents()==1,"actual fee and partial-payment preview match the committed account");
        await Reset(); await db.ExecuteCommandAsync("UPDATE rentals SET pending_surcharge=10,pending_surcharge_period='2026-10-01',pending_surcharge_rent_base=100 WHERE rental_id=100");
        dto=await Dto(110); dto.SurchargeAction="next_payment";
        await payments.CreatePaymentWithMovementAsync(dto);
        check(await Net()==-100 && await Rents()==2,"money consumed by the deferred fee is not reported as a false surplus");
        // A partially paid older month can remain due even with its following debit already issued.
        foreach (var action in new[] { "keep_month_pending", "close_partial_month" }) {
            await Reset();
            await db.ExecuteCommandAsync("INSERT account_movements(rental_id,movement_date,movement_type,concept,amount) VALUES(100,'2026-09-01','DEBITO','Alquiler Septiembre 2026',100)");
            await balances.RebuildForRentalAsync(100);
            dto=await Dto(50); decision=await Consult(dto,"partial_payment");
            var option=decision.Options.Single(o=>o.Action==action);
            check(option.Debits.Count==0 && option.ResultingBalance==-150
                && option.PreviousBalance==(action=="keep_month_pending" ? 0 : -50),
                "partial older month previews the chosen prior balance without duplicating its existing following rent");
            await Confirm(dto,decision,action);
            await VerifyOutcome(option,"older partial month "+action);
        }

        // A later manual credit must update all readers instead of keeping a stale chosen month.
        await Reset(); dto=await Dto(110); decision=await Consult(dto,"surplus");
        await Confirm(dto,decision,"credit_next_month");
        await db.ExecuteCommandAsync("INSERT account_movements(rental_id,movement_date,movement_type,concept,amount) VALUES(100,'2026-10-06','CREDITO','Ajuste manual',90)");
        await balances.RebuildForRentalAsync(100);
        check(await db.ExecuteScalarAsync("SELECT dbo.GetPaymentCollectionMonth(100)") is DBNull,
            "a manual subsequent credit invalidates the old collection choice");
        check(Convert.ToDateTime((await new DaoClient(db).GetClients()).Select("client_id=100").Single()["next_payment_day"])==new DateTime(2026,12,1),
            "manual settlement moves next payment forward after rebuild");
        await Reset(); // Leave fixture 100 without payment decisions for subsequent reactivation checks.

    }
}
