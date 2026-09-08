using System.Data;
using GuardeSoftwareAPI.Dao;
using Quartz;
using GuardeSoftwareAPI.Services.notification;

[DisallowConcurrentExecution]
public class ApplyInterestsJob : IJob
{
    private readonly ILogger<ApplyInterestsJob> _logger;
    private readonly DaoRental _daoRental;
    private readonly INotificationService _notificationService;

    public ApplyInterestsJob(
        ILogger<ApplyInterestsJob> logger,
        AccessDB accessDB,
        INotificationService notificationService)
    {
        _logger = logger;
        _daoRental = new DaoRental(accessDB);
        _notificationService = notificationService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Iniciando Job de Gestión de Mora. Hora: {time}", DateTimeOffset.Now);

        const int TERMINATION_THRESHOLD = 4;

        try
        {
            DataTable allRentals = await _daoRental.GetAllActiveRentalsWithStatusAsync();
            _logger.LogInformation("Procesando {count} alquileres activos.", allRentals.Rows.Count);
            int affectedRentals = 0;
            decimal totalInterests = 0;

            foreach (DataRow row in allRentals.Rows)
            {
                var rentalId = Convert.ToInt32(row["rental_id"]);
                var monthsUnpaid = Convert.ToInt32(row["months_unpaid"]);
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
                    var newMonthsUnpaid = monthsUnpaid + 1;
                    _logger.LogWarning("Cliente del alquiler ID {rentalId} está en mora...", rentalId);

                    // Si existe registro CMB con débitos, usamos la porción impaga (descontando pagos parciales).
                    // Si no hay CMB aún, usamos la cuota actual como fallback.
                    var interestAmount = taxableBase * 0.10m;
                    
                    var roundedInterest = RoundInterestToNearestHundredDown(interestAmount);

                    await _daoRental.IncrementUnpaidMonthsAndSaveInterestAsync(
                        rentalId,
                        roundedInterest,
                        lateRentBase,
                        DateTime.Today);
                    affectedRentals++;
                    totalInterests += roundedInterest;
                    _logger.LogInformation("Interés de ${amount} aplicado al alquiler ID {rentalId}. (Base Imponible: ${baseImponible}, Método Preferido: {method})", roundedInterest, rentalId, taxableBase, preferredMethod);

                    if (newMonthsUnpaid >= TERMINATION_THRESHOLD)
                    {
                        _logger.LogError("¡ACCIÓN CRÍTICA! El alquiler ID {rentalId} ha alcanzado {newMonthsUnpaid} meses de mora.", rentalId, newMonthsUnpaid);
                    }
                }
            }

            if (affectedRentals > 0)
            {
                string period = DateTime.Today.ToString("MM/yyyy");
                await _notificationService.CreateEventAsync(
                    sourceType: "interests_applied",
                    severity: "info",
                    title: "Intereses aplicados",
                    message: $"Se aplicaron intereses a {affectedRentals} alquileres por un total de {totalInterests:C0} para el período {period}.",
                    actionUrl: "/finances",
                    notificationKey: $"interests-applied:{DateTime.Today:yyyy-MM}");
            }
            _logger.LogInformation("Job de Gestión de Mora finalizado con éxito.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ocurrió un error crítico en el Job de Gestión de Mora.");
        }
    }

    private decimal RoundInterestToNearestHundredDown(decimal amount)
    {
        if (amount <= 0) return 0;

        return Math.Floor(amount / 100m) * 100m;
    }
}
