using System.Reflection;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.MonthlyInterest;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Services.monthlyInterest;
using GuardeSoftwareAPI.Services.notification;
using GuardeSoftwareAPI.Services.payment;
using GuardeSoftwareAPI.Utils;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Intereses por mora configurables (2026-11-10): regla y redondeo, migración, tolerancia sin
/// tablas, reglas de edición de Configuración, job del día configurado (una vez por mes) y el
/// recargo calculado al registrar un pago con el porcentaje del mes.
/// Uses the disposable database of Program.cs right after Ariadna's debits were rebuilt:
/// September 2026 has 232000 unpaid rent and 48700 unpaid interests (taxable base 280700).
/// </summary>
public static class InterestSettingsChecks
{
    public static async Task RunAsync(AccessDB db, PaymentService payments, PaymentStateService stateService, Action<bool, string> check)
    {
        // --- Regla pura ---------------------------------------------------------------------
        check(InterestRule.Calculate(280700m, 10m) == 28000m, "10 % keeps the historical amount (280700 -> 28000)");
        check(InterestRule.Calculate(280700m, 12.5m) == 35000m, "12.5 % truncates to hundreds (35087.5 -> 35000)");
        check(InterestRule.Calculate(999m, 10m) == 0m && InterestRule.Calculate(280700m, 0m) == 0m, "small base or 0 % gives no surcharge");
        check(LatePaymentSurchargeCalculator.Calculate(232000m, 48700m) == 28000m &&
              LatePaymentSurchargeCalculator.Calculate(232000m, 48700m, 12.5m) == 35000m,
            "payment calculator defaults to 10 % and accepts the month percentage");
        check(InterestRule.Default.ApplyDay == 11 && InterestRule.Default.CutoffDay == 10 && InterestRule.Default.Percentage == 10m,
            "default month: day 11, term until the 10th, 10 %");
        var nov5 = new DateTime(2026, 11, 5);
        check(InterestRule.IsMonthClosed(new(2026, 10, 1), 11, nov5, false), "past month is closed");
        check(!InterestRule.IsMonthClosed(new(2026, 11, 1), 11, nov5, false), "current month before its day is open");
        check(InterestRule.IsMonthClosed(new(2026, 11, 1), 5, nov5, false), "current month on its day is closed");
        check(!InterestRule.IsMonthClosed(new(2026, 12, 1), 2, nov5, false), "future month is open");
        check(InterestRule.IsMonthClosed(new(2026, 12, 1), 20, nov5, true), "an applied month is closed");

        // --- Sin migración: el job y los pagos siguen con el valor por defecto ---------------
        var dao = new DaoMonthlyInterest(db);
        var beforeMigration = await dao.GetRuleForMonthAsync(new DateTime(2027, 3, 1));
        check(beforeMigration == InterestRule.Default, "without the tables the month uses the default rule");
        check(!await dao.IsMonthAppliedAsync(new DateTime(2027, 3, 1)), "without the tables no month is recorded as applied");

        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "interest-settings-migration.sql"));
        await db.ExecuteCommandAsync(migration);
        await db.ExecuteCommandAsync(migration);
        check(true, "interest settings migration applies twice");

        // --- Configuración: reglas de edición -----------------------------------------------
        DateTime clock = nov5;
        var service = new MonthlyInterestService(db, NullLogger<MonthlyInterestService>.Instance, () => clock);
        async Task<string> Fails(Func<Task> action)
        {
            try { await action(); return "no error"; }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ex.GetType().Name; }
        }

        var november = await service.CreateSettingAsync(new CreateMonthlyInterestDTO { EffectiveDate = "2026-11", Percentage = 12.5m, ApplyDay = 15 });
        check(november.Id > 0 && november.ApplyDay == 15 && november.CutoffDay == 14 && november.Percentage == 12.5m,
            "current month can be configured with a day after today");
        check(await Fails(() => service.CreateSettingAsync(new() { EffectiveDate = "2026-11", Percentage = 9m, ApplyDay = 20 }))
              == nameof(InvalidOperationException), "a month cannot be configured twice");
        check(await Fails(() => service.CreateSettingAsync(new() { EffectiveDate = "2026-10", Percentage = 9m, ApplyDay = 20 }))
              == nameof(InvalidOperationException), "a past month cannot be configured");
        check(await Fails(() => service.CreateSettingAsync(new() { EffectiveDate = "2027-01", Percentage = 9m, ApplyDay = 29 }))
              == nameof(ArgumentException), "day 29 is rejected (2 to 28)");
        check(await Fails(() => service.CreateSettingAsync(new() { EffectiveDate = "2027-01", Percentage = 100.5m, ApplyDay = 10 }))
              == nameof(ArgumentException), "more than 100 % is rejected");
        check(await Fails(() => service.UpdateSettingAsync(november.Id, new() { Percentage = 12.5m, ApplyDay = 5 }))
              == nameof(ArgumentException), "the current month cannot move its day to today or earlier");
        check(await service.UpdateSettingAsync(november.Id, new() { Percentage = 11m, ApplyDay = 14 }),
            "the current month can still change before its day");
        var overview = await service.GetOverviewAsync();
        check(overview.DefaultPercentage == 10m && overview.DefaultApplyDay == 11 && overview.Today == "2026-11-05" &&
              overview.Settings.Single(s => s.Id == november.Id) is { Percentage: 11m, ApplyDay: 14, IsLocked: false },
            "overview returns defaults, server date and the open month");
        clock = new DateTime(2026, 11, 14);
        check((await service.GetOverviewAsync()).Settings.Single(s => s.Id == november.Id).IsLocked,
            "the month locks on its apply day");
        check(await Fails(() => service.UpdateSettingAsync(november.Id, new() { Percentage = 20m, ApplyDay = 20 }))
              == nameof(InvalidOperationException), "a locked month cannot be edited");
        check(await Fails(() => service.DeleteSettingAsync(november.Id)) == nameof(InvalidOperationException),
            "a locked month cannot be deleted");
        clock = new DateTime(2026, 11, 12);
        var december = await service.CreateSettingAsync(new() { EffectiveDate = "2026-12", Percentage = 0m, ApplyDay = 20 });
        check(december.Percentage == 0m, "0 % is accepted (no interest that month)");
        var currentNoDelete = await service.CreateSettingAsync(new() { EffectiveDate = "2027-02", Percentage = 5m, ApplyDay = 3 });
        clock = new DateTime(2027, 2, 2);
        check(await service.DeleteSettingAsync(currentNoDelete.Id), "a future-day month can be deleted while its day is ahead");

        // --- Job: corre sólo el día configurado y una sola vez por mes -----------------------
        // March 2027: 12.5 % on the 15th. April 2027: no row (10 % on the 11th). May 2027: 0 % on the 9th.
        clock = new DateTime(2027, 1, 10);
        var march = await service.CreateSettingAsync(new() { EffectiveDate = "2027-03", Percentage = 12.5m, ApplyDay = 15 });
        await service.CreateSettingAsync(new() { EffectiveDate = "2027-05", Percentage = 0m, ApplyDay = 9 });
        var notifications = DispatchProxy.Create<INotificationService, InterestNotificationStub>();
        var job = new ApplyInterestsJob(NullLogger<ApplyInterestsJob>.Instance, db, notifications);
        async Task<decimal> Bag() => Convert.ToDecimal(await db.ExecuteScalarAsync("SELECT ISNULL(pending_surcharge, 0) FROM rentals WHERE rental_id = 1"));
        async Task ResetBag() => await db.ExecuteCommandAsync(
            "UPDATE rentals SET pending_surcharge = 0, pending_surcharge_rent_base = NULL, pending_surcharge_period = NULL WHERE rental_id = 1");

        await ResetBag();
        check(!await job.RunAsync(new DateTime(2027, 3, 11)) && await Bag() == 0m,
            "configured month: the default day 11 no longer applies interests");
        check(!await job.RunAsync(new DateTime(2027, 3, 14)) && await Bag() == 0m, "the day before the configured day does nothing");
        check(await job.RunAsync(new DateTime(2027, 3, 15)) && await Bag() == 35000m,
            "on the configured day the job uses 12.5 % (280700 -> 35000)");
        check(Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT pending_surcharge_period FROM rentals WHERE rental_id = 1")) == new DateTime(2027, 3, 1),
            "the surcharge period is the applied month");
        check(await dao.IsMonthAppliedAsync(new DateTime(2027, 3, 1)) &&
              Convert.ToDecimal(await db.ExecuteScalarAsync("SELECT total_amount FROM monthly_interest_runs WHERE period = '2027-03-01'")) == 35000m,
            "the applied month is recorded with its total");
        await ResetBag();
        check(!await job.RunAsync(new DateTime(2027, 3, 15)) && await Bag() == 0m,
            "a second run in the same month does not recreate a resolved surcharge");
        clock = new DateTime(2027, 3, 10);
        check(await Fails(() => service.UpdateSettingAsync(march.Id, new() { Percentage = 20m, ApplyDay = 20 }))
              == nameof(InvalidOperationException), "an applied month stays locked even before its day by the clock");

        // Pregunta del usuario: configurar el día 13 (con el 10 % de siempre) mueve el proceso del 11 al 13.
        clock = new DateTime(2027, 5, 20);
        await service.CreateSettingAsync(new() { EffectiveDate = "2027-06", Percentage = 10m, ApplyDay = 13 });
        await ResetBag();
        check(!await job.RunAsync(new DateTime(2027, 6, 11)) && !await job.RunAsync(new DateTime(2027, 6, 12)) && await Bag() == 0m &&
              !await dao.IsMonthAppliedAsync(new DateTime(2027, 6, 1)),
            "day 13 configured: nothing happens on the 11th or the 12th");
        check(await job.RunAsync(new DateTime(2027, 6, 13)) && await Bag() == 28000m &&
              Convert.ToDateTime(await db.ExecuteScalarAsync("SELECT pending_surcharge_period FROM rentals WHERE rental_id = 1")) == new DateTime(2027, 6, 1),
            "day 13 configured: the usual process runs on the 13th (10 % of 280700 -> 28000)");
        check((await dao.GetRuleForMonthAsync(new DateTime(2027, 6, 1))).CutoffDay == 12,
            "day 13 configured: payments on the 11th and 12th are still within term");
        clock = new DateTime(2027, 1, 10);

        await ResetBag();
        check(!await job.RunAsync(new DateTime(2027, 4, 10)) && await job.RunAsync(new DateTime(2027, 4, 11)) && await Bag() == 28000m,
            "a month without configuration keeps 10 % on the 11th");
        check(Convert.ToBoolean(await db.ExecuteScalarAsync("SELECT used_default FROM monthly_interest_runs WHERE period = '2027-04-01'")),
            "the run records that the default rule was used");

        await ResetBag();
        check(await job.RunAsync(new DateTime(2027, 5, 9)) && await Bag() == 0m &&
              Convert.ToInt32(await db.ExecuteScalarAsync("SELECT affected_rentals FROM monthly_interest_runs WHERE period = '2027-05-01'")) == 0,
            "a 0 % month records the run without saving surcharges");
        check(InterestNotificationStub.Messages.Count == 3 &&
              (InterestNotificationStub.Messages[0].Contains("12,5 %", StringComparison.Ordinal) ||
               InterestNotificationStub.Messages[0].Contains("12.5 %", StringComparison.Ordinal)),
            "the notification states the applied percentage");

        // --- Registro de pago: el recargo sin bolsa usa el porcentaje del mes ---------------
        await ResetBag();
        await dao.CreateAsync(new DateTime(2026, 9, 1), 12.5m, 11); // direct insert: September 2026 is in the past for the service
        var dto = new CreatePaymentTransaction
        {
            ClientId = 1, PaymentMethodId = 1, Amount = 1000m, Date = new DateTime(2026, 9, 14),
            SkipFutureProjection = true, SurchargeAction = "next_payment",
            ExpectedPaymentStateToken = (await stateService.GetSnapshotAsync(1)).Token
        };
        try { await payments.CreatePaymentWithMovementAsync(dto); }
        catch (PaymentDecisionRequiredException ex)
        {
            dto.FutureDebitAction = "keep_month_pending";
            dto.PaymentDecisionToken = ex.Details.DecisionToken;
            await payments.CreatePaymentWithMovementAsync(dto);
        }
        var fee = await db.ExecuteScalarAsync("SELECT amount FROM account_movements WHERE concept = 'Interés por mora de Septiembre 2026'");
        check(fee != null && Convert.ToDecimal(fee) == 35000m,
            "registering a late payment charges 12.5 % of 280700 (35000), not the fixed 10 % (28000)");
    }
}

public class InterestNotificationStub : DispatchProxy
{
    public static readonly List<string> Messages = [];

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "CreateEventAsync")
        {
            var messageIndex = Array.FindIndex(method.GetParameters(), p => p.Name == "message");
            if (messageIndex >= 0 && args?[messageIndex] is string message) Messages.Add(message);
            return Task.FromResult(0L);
        }
        throw new NotSupportedException(method?.Name);
    }
}
