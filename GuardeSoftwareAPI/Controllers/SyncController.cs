using GuardeSoftwareAPI.Dtos.Sync;
using GuardeSoftwareAPI.Services.sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Services.payment;
using Microsoft.Data.SqlClient;

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

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus([FromServices] AccessDB db)
        {
            try
            {
                using var connection = db.GetConnectionClose();
                await connection.OpenAsync(HttpContext.RequestAborted);
                using var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.offline_payment_receipts', N'U') IS NULL THEN 0 ELSE 1 END", connection);
                var ready = Convert.ToInt32(await command.ExecuteScalarAsync(HttpContext.RequestAborted)) == 1;
                Response.Headers.CacheControl = "no-store";
                return Ok(new { healthy = true, offlinePaymentsReady = ready });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Offline availability probe failed");
                return StatusCode(503, new { healthy = false });
            }
        }

        [HttpGet("payment-state/{clientId:int}")]
        public async Task<IActionResult> GetPaymentReview(int clientId, [FromServices] IPaymentStateService stateService)
        {
            var state = await stateService.GetSnapshotAsync(clientId);
            var snapshot = await _syncService.GetSnapshotAsync();
            var client = snapshot.Clients.SingleOrDefault(c => c.Id == clientId);
            if (client == null) return NotFound(new { message = "El cliente no está activo. El cobro local se conserva." });
            Response.Headers.CacheControl = "no-store";
            return Ok(new { stateToken = state.Token, client });
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
                Response.Headers.CacheControl = "no-store";
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
            if (request.Payments.Count > 100 || request.Payments.Select(p => p.LocalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Payments.Count)
                return BadRequest(new { message = "Enviá hasta 100 cobros con identificadores distintos por lote." });

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
