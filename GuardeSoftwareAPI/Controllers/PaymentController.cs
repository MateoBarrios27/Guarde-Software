using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.payment;
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Payment;
using Microsoft.AspNetCore.Authorization;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
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

                bool result = await _paymentService.CreatePaymentWithMovementAsync(dto);

                if (result)
                    return Ok(new { Message = "Payment and account movement created successfully." });

                return BadRequest("Could not create payment transaction.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating payment transaction: {ex.Message}");
            }
        }

    }
}
