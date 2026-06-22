using System.Data;
using GuardeSoftwareAPI.Dao;
using Quartz;

[DisallowConcurrentExecution]
public class ApplyInterestsJob : IJob
{
    private readonly ILogger<ApplyInterestsJob> _logger;
    private readonly DaoRental _daoRental;

    public ApplyInterestsJob(ILogger<ApplyInterestsJob> logger, AccessDB accessDB)
    {
        _logger = logger;
        _daoRental = new DaoRental(accessDB); 
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Iniciando Job de Gestión de Mora. Hora: {time}", DateTimeOffset.Now);

        const int TERMINATION_THRESHOLD = 4;

        try
        {
            DataTable allRentals = await _daoRental.GetAllActiveRentalsWithStatusAsync();
            _logger.LogInformation("Procesando {count} alquileres activos.", allRentals.Rows.Count);

            foreach (DataRow row in allRentals.Rows)
            {
                var rentalId = Convert.ToInt32(row["rental_id"]);
                var balance = Convert.ToDecimal(row["balance"]);
                var monthsUnpaid = Convert.ToInt32(row["months_unpaid"]);
                var currentRent = row["CurrentRent"] != DBNull.Value ? Convert.ToDecimal(row["CurrentRent"]) : 0;
                
                var currentInterests = row["CurrentInterests"] != DBNull.Value ? Convert.ToDecimal(row["CurrentInterests"]) : 0;
                var monthlyDebits = row["MonthlyDebits"] != DBNull.Value ? Convert.ToDecimal(row["MonthlyDebits"]) : 0;
                
                var preferredMethod = row["PreferredPaymentMethod"].ToString();

                if (balance > 0)
                {
                    var newMonthsUnpaid = monthsUnpaid + 1;
                    _logger.LogWarning("Cliente del alquiler ID {rentalId} está en mora...", rentalId);

                    decimal cuotaBase = monthlyDebits > 0 ? monthlyDebits : currentRent;
                    decimal baseImponible = cuotaBase + currentInterests;

                    var interestAmount = baseImponible * 0.10m;
                    
                    var roundedInterest = RoundInterestToNearestHundredDown(interestAmount);

                    await _daoRental.IncrementUnpaidMonthsAndSaveInterestAsync(rentalId, roundedInterest);
                    _logger.LogInformation("Interés de ${amount} aplicado al alquiler ID {rentalId}. (Base Imponible: ${baseImponible}, Método Preferido: {method})", roundedInterest, rentalId, baseImponible, preferredMethod);

                    if (newMonthsUnpaid >= TERMINATION_THRESHOLD)
                    {
                        _logger.LogError("¡ACCIÓN CRÍTICA! El alquiler ID {rentalId} ha alcanzado {newMonthsUnpaid} meses de mora.", rentalId, newMonthsUnpaid);
                    }
                }
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