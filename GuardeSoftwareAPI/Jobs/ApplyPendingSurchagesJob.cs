using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using Quartz;

[DisallowConcurrentExecution]
public class ApplyPendingSurchargesJob : IJob
{
    private readonly ILogger<ApplyPendingSurchargesJob> _logger;
    private readonly DaoRental _daoRental;
    private readonly IClientMonthBalanceService _clientMonthBalanceService;

    public ApplyPendingSurchargesJob(
        ILogger<ApplyPendingSurchargesJob> logger,
        AccessDB accessDB,
        IClientMonthBalanceService clientMonthBalanceService)
    {
        _logger = logger;
        _daoRental = new DaoRental(accessDB);
        _clientMonthBalanceService = clientMonthBalanceService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Iniciando Job de Aplicacion de Recargos Pendientes. Hora: {time}", DateTimeOffset.Now);

        try
        {
            await _daoRental.ApplyPendingSurchargesAsync();
            await _clientMonthBalanceService.RebuildAllActiveRentalsAsync();
            _logger.LogInformation("Job de Aplicacion de Recargos Pendientes finalizado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aplicar recargos pendientes.");
        }
    }
}
