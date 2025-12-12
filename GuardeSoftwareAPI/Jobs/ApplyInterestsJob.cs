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
        string concept = $"Interés por mora - {DateTime.Now:MMMM yyyy}";

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

                // Query: SUM(CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END)
                // So, balance > 0 means the client owes money.
                if (balance > 0)
                {
                    var newMonthsUnpaid = monthsUnpaid + 1;
                    _logger.LogWarning("Cliente del alquiler ID {rentalId} está en mora...", rentalId);

                    var interestAmount = Math.Round(balance * 0.10m, 2);
                    var roundedInterest = RoundUpToNearest100(interestAmount);

                    await _daoRental.IncrementUnpaidMonthsAndApplyInterestAsync(rentalId, roundedInterest, concept);
                    _logger.LogInformation("Interés de ${amount} aplicado al alquiler ID {rentalId}.", roundedInterest, rentalId);

                    if (newMonthsUnpaid >= TERMINATION_THRESHOLD)
                    {
                        _logger.LogError("¡ACCIÓN CRÍTICA! El alquiler ID {rentalId} ha alcanzado {newMonthsUnpaid} meses de mora.", rentalId, newMonthsUnpaid);
                        // Logic to notify admin or take further action can be added here.
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

    private decimal RoundUpToNearest100(decimal amount)
    {
        if (amount == 0) return 0;
        return Math.Ceiling(amount / 100.0m) * 100;
    }
}