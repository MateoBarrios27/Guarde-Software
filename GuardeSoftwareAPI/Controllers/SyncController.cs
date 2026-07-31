using GuardeSoftwareAPI.Dtos.Sync;
using GuardeSoftwareAPI.Services.sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SyncController : ControllerBase
    {
        private readonly ISyncService _syncService;
        private readonly ILogger<SyncController> _logger;

        public SyncController(ISyncService syncService, ILogger<SyncController> logger)
        {
            _syncService = syncService;
            _logger = logger;
        }

        /// <summary>
        /// Returns a compact snapshot of all data needed to work offline.
        /// The Angular client caches this in IndexedDB and can also download it as a .json file.
        /// </summary>
        [HttpGet("snapshot")]
        public async Task<IActionResult> GetSnapshot()
        {
            try
            {
                var snapshot = await _syncService.GetSnapshotAsync();
                return Ok(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sync snapshot");
                return StatusCode(500, new { message = "Error al generar el snapshot de sincronización.", error = ex.Message });
            }
        }

        /// <summary>
        /// Processes a batch of payments recorded offline and syncs them to the server.
        /// Payments are processed in chronological order.
        /// </summary>
        [HttpPost("payments")]
        public async Task<IActionResult> SyncPayments([FromBody] SyncPaymentsRequestDto request)
        {
            if (request == null || request.Payments == null || request.Payments.Count == 0)
                return BadRequest(new { message = "No hay pagos para sincronizar." });

            try
            {
                _logger.LogInformation("Syncing {Count} offline payment(s)", request.Payments.Count);
                var result = await _syncService.ProcessOfflinePaymentsAsync(request);
                _logger.LogInformation("Sync completed: {Success} success, {Failure} failures", result.SuccessCount, result.FailureCount);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing offline payments");
                return StatusCode(500, new { message = "Error al sincronizar pagos offline.", error = ex.Message });
            }
        }
    }
}
