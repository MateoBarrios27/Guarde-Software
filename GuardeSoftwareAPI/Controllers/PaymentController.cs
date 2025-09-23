using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.payment;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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

        [HttpPost]
        public async Task<IActionResult> CreatePayment([FromBody] Payment payment)
        {
            try
            {
                if (payment == null)
                    return BadRequest("Payment method is null.");
                bool isCreated = await _paymentService.CreatePayment(payment);
                if (!isCreated)
                    return StatusCode(500, "Failed to create the payment.");
                return CreatedAtAction(nameof(GetPaymentById), new { id = payment.Id }, payment);
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating the payment: {ex.Message}");
            }
        }
    }
}
