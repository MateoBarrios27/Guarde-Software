using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.accountMovement;
using Quartz;

namespace GuardeSoftwareAPI.Jobs
{
    [DisallowConcurrentExecution]
    public class ApplyDebitsJob : IJob
    {
        private readonly ILogger<ApplyDebitsJob> _logger;
        private readonly AccountMovementService _accountMovementService;

        public ApplyDebitsJob(ILogger<ApplyDebitsJob> logger, AccountMovementService accountMovementService)
        {
            _logger = logger;
            _accountMovementService = accountMovementService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("--- Initializing debit applier job ---");
            try
            {
                await _accountMovementService.ApplyMonthlyDebitsAsync();
                // Optionally, you can log success or other details here
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "An error has ocurred during debits apply job.");
                // Optionally, throw an exception to indicate job failure
            }
            _logger.LogInformation("--- Debits apply job complete ---");
        }
    }
}