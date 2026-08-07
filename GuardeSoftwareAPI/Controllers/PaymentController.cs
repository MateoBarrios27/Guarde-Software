using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.payment;
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IHubContext<PaymentPresenceHub> _paymentPresenceHub;
        private readonly DaoUser _daoUser;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            IHubContext<PaymentPresenceHub> paymentPresenceHub,
            DaoUser daoUser,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _paymentPresenceHub = paymentPresenceHub;
            _daoUser = daoUser;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<Payment>>> GetPayments()
        {
            try
            {
                List<Payment> payments = await _paymentService.GetPaymentsList();

                return Ok(payments);
            }

            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }

            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting payments: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPaymentById(int id)
        {
            try
            {
                Payment payment = await _paymentService.GetPaymentById(id);
                if (payment == null)
                    return NotFound("No payment found with the given ID.");

                return Ok(payment);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("ByClientId/{clientId}")]
        public async Task<IActionResult> GetPaymentsByClientId(int clientId)
        {
            try
            {
                List<Payment> payments = await _paymentService.GetPaymentsByClientId(clientId);
                if (payments == null || payments.Count == 0)
                    return NotFound("No payment found with the given ID.");

                return Ok(payments);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailedPayments()
        {
            try
            {
                List<DetailedPaymentDto> payments = await _paymentService.GetDetailedPaymentsAsync();

                return Ok(payments);
            }

            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }

            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting payments: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Payment>> CreatePaymentTransaction([FromBody] CreatePaymentTransaction dto)
        {
            try
            {
               if (dto == null)
                    return BadRequest("Payment data is required.");

                var actor = await GetCurrentActorAsync();
                bool result = await _paymentService.CreatePaymentWithMovementAsync(dto, actor.DisplayName, actor.UserName);

                if (result)
                {
                    await NotifyPaymentCompletedAsync(dto, actor);

                    return Ok(new { Message = "Payment and account movement created successfully." });
                }

                return BadRequest("Could not create payment transaction.");
            }
            catch (PaymentConflictException conflict)
            {
                return Conflict(conflict.Details);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating payment transaction: {ex.Message}");
            }
        }

        private async Task<(string DisplayName, string UserName)> GetCurrentActorAsync()
        {
            var userName = User.FindFirst("username")?.Value
                ?? User.Identity?.Name
                ?? "usuario";
            var displayName = userName;
            var identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!string.IsNullOrWhiteSpace(identityUserId))
            {
                var userTable = await _daoUser.GetUserByIdentityUserId(identityUserId);
                if (userTable.Rows.Count > 0)
                {
                    var row = userTable.Rows[0];
                    userName = row["username"]?.ToString() ?? userName;
                    displayName = row["first_name"]?.ToString() ?? userName;
                }
            }

            return (string.IsNullOrWhiteSpace(displayName) ? userName : displayName, userName);
        }

        private async Task NotifyPaymentCompletedAsync(
            CreatePaymentTransaction dto,
            (string DisplayName, string UserName) actor)
        {
            try
            {
                await _paymentPresenceHub.Clients
                    .Group(PaymentPresenceHub.ClientGroupName(dto.ClientId))
                    .SendAsync("PaymentCompleted", new PaymentCompletedNotice
                    {
                        ClientId = dto.ClientId,
                        PayerName = actor.DisplayName,
                        PayerUserName = actor.UserName,
                        Amount = dto.Amount,
                        PaymentDate = dto.Date,
                        RecordedAtUtc = DateTime.UtcNow,
                        Concept = dto.Concept
                    });
            }
            catch (Exception ex)
            {
                // The transaction has already committed; a transient hub failure must not turn it into a failed payment.
                _logger.LogWarning(ex, "Payment {ClientId} was saved but its presence notification could not be delivered", dto.ClientId);
            }
        }

        [HttpDelete("{id}")]
        // [Authorize(Role = "Admin")] // Recomendable to restrict this action to admin users only
        public async Task<IActionResult> DeletePayment(int id)
        {
            try
            {
                bool success = await _paymentService.DeletePaymentAsync(id);
                
                if (!success) 
                    return NotFound(new { message = "El pago no existe o ya fue eliminado." });

                return Ok(new { message = "Pago y movimiento eliminados correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
            }
        }

    }
}
