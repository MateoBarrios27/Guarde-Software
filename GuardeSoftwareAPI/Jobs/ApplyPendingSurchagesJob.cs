using GuardeSoftwareAPI.Dao;
using Quartz;

[DisallowConcurrentExecution]
public class ApplyPendingSurchargesJob : IJob
{
    private readonly ILogger<ApplyPendingSurchargesJob> _logger;
    private readonly DaoRental _daoRental;

    public ApplyPendingSurchargesJob(ILogger<ApplyPendingSurchargesJob> logger, AccessDB accessDB)
    {
        _logger = logger;
        _daoRental = new DaoRental(accessDB);
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Iniciando Job de Aplicación de Recargos Pendientes. Hora: {time}", DateTimeOffset.Now);

        try
        {
            await _daoRental.ApplyPendingSurchargesAsync();
            _logger.LogInformation("Job de Aplicación de Recargos Pendientes finalizado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aplicar recargos pendientes.");
        }
    }
}   
