using GuardeSoftwareAPI.Dao;
using Quartz;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;

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
        _logger.LogInformation("Iniciando Job de Gestión de Mora y Cobranzas. Hora: {time}", DateTimeOffset.Now);

        const int TERMINATION_THRESHOLD = 4; // Límite de 4 meses para rescindir

        try
        {
            DataTable allRentals = await _daoRental.GetAllActiveRentalsWithStatusAsync();
            _logger.LogInformation("Procesando {count} alquileres activos.", allRentals.Rows.Count);

            string concept = $"Interés por mora - {DateTime.Now:MMMM yyyy}";

            foreach (DataRow row in allRentals.Rows)
            {
                var rentalId = Convert.ToInt32(row["rental_id"]);
                var balance = Convert.ToDecimal(row["balance"]);
                var monthsUnpaid = Convert.ToInt32(row["months_unpaid"]);
                var currentRent = row["CurrentRent"] != DBNull.Value ? Convert.ToDecimal(row["CurrentRent"]) : 0;

                if (balance > 0)
                {
                    // CASO 1: EL CLIENTE ESTÁ EN MORA
                    var newMonthsUnpaid = monthsUnpaid + 1;
                    _logger.LogWarning("Cliente del alquiler ID {rentalId} está en mora. Meses impagos actualizados a: {newMonthsUnpaid}.", rentalId, newMonthsUnpaid);

                    var interestAmount = Math.Round(currentRent * 0.10m, 2);
                    await _daoRental.IncrementUnpaidMonthsAndApplyInterestAsync(rentalId, interestAmount, concept);
                    _logger.LogInformation("Interés de ${amount} aplicado al alquiler ID {rentalId}.", interestAmount, rentalId);

                    if (newMonthsUnpaid >= TERMINATION_THRESHOLD)
                    {
                        // _logger.LogError("¡ACCIÓN CRÍTICA! El alquiler ID {rentalId} ha alcanzado {newMonthsUnpaid} meses de mora. Se rescinde el contrato.", rentalId, newMonthsUnpaid);
                        // await _daoRental.TerminateContractAsync(rentalId);
                        // Opcional: Enviar una notificación
                    }
                }
                else if (monthsUnpaid > 0)
                {
                    // CASO 2: EL CLIENTE ESTABA EN MORA, PERO SE PUSO AL DÍA
                    _logger.LogInformation("Cliente del alquiler ID {rentalId} se ha puesto al día. Reseteando contador de meses impagos a 0.", rentalId);
                    await _daoRental.ResetUnpaidMonthsAsync(rentalId);
                }
            }
            _logger.LogInformation("Job de Gestión de Mora finalizado con éxito.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ocurrió un error crítico en el Job de Gestión de Mora.");
        }
    }
}