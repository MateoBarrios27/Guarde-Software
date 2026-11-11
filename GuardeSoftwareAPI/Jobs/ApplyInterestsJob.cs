using System.Data;
using GuardeSoftwareAPI.Dao;
using Quartz;
using GuardeSoftwareAPI.Services.notification;
using GuardeSoftwareAPI.Utils;

[DisallowConcurrentExecution]
public class ApplyInterestsJob : IJob
{
    private readonly ILogger<ApplyInterestsJob> _logger;
    private readonly DaoRental _daoRental;
    private readonly DaoMonthlyInterest _daoMonthlyInterest;
    private readonly INotificationService _notificationService;

    public ApplyInterestsJob(
        ILogger<ApplyInterestsJob> logger,
        AccessDB accessDB,
        INotificationService notificationService)
    {
        _logger = logger;
        _daoRental = new DaoRental(accessDB);
        _daoMonthlyInterest = new DaoMonthlyInterest(accessDB);
        _notificationService = notificationService;
    }

    // Quartz lo dispara todos los días a las 4:00; sólo actúa el día configurado del mes.
    public Task Execute(IJobExecutionContext context) => RunAsync(DateTime.Today);

    /// <summary>
    /// Aplica los intereses del mes de <paramref name="today"/> si hoy es su día de aplicación
    /// (Configuración → Intereses por mora; sin configuración, el día 11 y el 10 %) y si el mes
    /// todavía no se aplicó. Devuelve true cuando procesó el mes.
    /// </summary>
    public async Task<bool> RunAsync(DateTime today)
    {
        today = today.Date;
        try
        {
            MonthlyInterestRule rule = await _daoMonthlyInterest.GetRuleForMonthAsync(today);
            if (today.Day != rule.ApplyDay)
            {
                _logger.LogDebug("Job de Gestión de Mora: hoy no corresponde (día {day}; se aplica el {applyDay}).", today.Day, rule.ApplyDay);
                return false;
            }
            if (await _daoMonthlyInterest.IsMonthAppliedAsync(today))
            {
                _logger.LogInformation("Job de Gestión de Mora: los intereses de {period:MM/yyyy} ya se aplicaron.", today);
                return false;
            }
            return await ApplyAsync(today, rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ocurrió un error crítico en el Job de Gestión de Mora.");
            return false;
        }
    }

    private async Task<bool> ApplyAsync(DateTime today, MonthlyInterestRule rule)
    {
        _logger.LogInformation("Iniciando Job de Gestión de Mora. Hora: {time}. Interés del mes: {percentage} % ({source}).",
            DateTimeOffset.Now, rule.Percentage, rule.IsDefault ? "por defecto" : "configurado");

        if (rule.Percentage <= 0m)
        {
            // Un 0 % configurado significa «sin intereses este mes»: no se guarda recargo.
            await _daoMonthlyInterest.RecordRunAsync(today, rule, 0, 0m);
            _logger.LogInformation("Job de Gestión de Mora: {period:MM/yyyy} está configurado sin intereses.", today);
            return true;
        }

        {
            DataTable allRentals = await _daoRental.GetAllActiveRentalsWithStatusAsync();
            _logger.LogInformation("Procesando {count} alquileres activos.", allRentals.Rows.Count);
            int affectedRentals = 0;
            decimal totalInterests = 0;

            foreach (DataRow row in allRentals.Rows)
            {
                var rentalId = Convert.ToInt32(row["rental_id"]);
                var currentRent = row["CurrentRent"] != DBNull.Value ? Convert.ToDecimal(row["CurrentRent"]) : 0;
                
                var currentInterests = row["CurrentInterests"] != DBNull.Value ? Convert.ToDecimal(row["CurrentInterests"]) : 0;
                var monthlyDebits = row["MonthlyDebits"] != DBNull.Value ? Convert.ToDecimal(row["MonthlyDebits"]) : 0;
                var unpaidMonthlyDebits = row["UnpaidMonthlyDebits"] != DBNull.Value ? Convert.ToDecimal(row["UnpaidMonthlyDebits"]) : 0;
                
                var preferredMethod = row["PreferredPaymentMethod"].ToString();
                // La existencia de un mes futuro no cancela una mora del mes actual.
                // La base solo incluye la porción vencida del alquiler actual y los intereses impagos.
                decimal lateRentBase = monthlyDebits > 0 ? unpaidMonthlyDebits : currentRent;
                decimal taxableBase = lateRentBase + currentInterests;

                if (taxableBase > 0)
                {
                    _logger.LogWarning("Cliente del alquiler ID {rentalId} está en mora...", rentalId);

                    // Si existe registro CMB con débitos, usamos la porción impaga (descontando pagos parciales).
                    // Si no hay CMB aún, usamos la cuota actual como fallback.
                    // Porcentaje del mes (Configuración → Intereses por mora; 10 % si no está configurado).
                    var interestAmount = taxableBase * rule.Percentage / 100m;

                    var roundedInterest = RoundInterestToNearestHundredDown(interestAmount);

                    await _daoRental.SavePendingSurchargeAsync(
                        rentalId,
                        roundedInterest,
                        lateRentBase,
                        today);
                    affectedRentals++;
                    totalInterests += roundedInterest;
                    _logger.LogInformation("Interés de ${amount} aplicado al alquiler ID {rentalId}. (Base Imponible: ${baseImponible}, Método Preferido: {method})", roundedInterest, rentalId, taxableBase, preferredMethod);

                }
            }

            // Se registra el mes aplicado: no se vuelve a aplicar y su configuración queda fija.
            await _daoMonthlyInterest.RecordRunAsync(today, rule, affectedRentals, totalInterests);

            if (affectedRentals > 0)
            {
                string period = today.ToString("MM/yyyy");
                await _notificationService.CreateEventAsync(
                    sourceType: "interests_applied",
                    severity: "info",
                    title: "Intereses aplicados",
                    message: $"Se aplicaron intereses del {rule.Percentage:0.##} % a {affectedRentals} alquileres por un total de {totalInterests:C0} para el período {period}.",
                    actionUrl: "/finances",
                    notificationKey: $"interests-applied:{today:yyyy-MM}");
            }
            _logger.LogInformation("Job de Gestión de Mora finalizado con éxito.");
            return true;
        }
    }

    private decimal RoundInterestToNearestHundredDown(decimal amount)
    {
        if (amount <= 0) return 0;

        return Math.Floor(amount / 100m) * 100m;
    }
}
