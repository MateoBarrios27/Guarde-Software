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
        public ActionResult<List<Payment>> GetPayments()
        {
            try
            {
                List<Payment> payments = _paymentService.GetPaymentsList();

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
        public IActionResult GetPaymentById(int id)
        {
            try
            {
                Payment payment = _paymentService.GetPaymentById(id);
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
        public IActionResult GetPaymentsByClientId(int clientId)
        {
            try
            {
                List<Payment> payments = _paymentService.GetPaymentsByClientId(clientId);
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
    }
}
