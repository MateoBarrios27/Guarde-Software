using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.paymentMethod;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class PaymentMethodController : ControllerBase
    {
        private readonly IPaymentMethodService _paymentMethodService;

        public PaymentMethodController(IPaymentMethodService paymentMethodService)
        {
            _paymentMethodService = paymentMethodService;
        }

        [HttpGet]
        public ActionResult<List<PaymentMethod>> GetPaymentMethods()
        {
            try
            {
                List<PaymentMethod> paymentMethods = _paymentMethodService.GetPaymentMethodsList();

                return Ok(paymentMethods);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting payment methods: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetPaymenMethodById(int id)
        {
            try
            {
                PaymentMethod paymentMethod = _paymentMethodService.GetPaymentMethodById(id);
                if (paymentMethod == null)
                    return NotFound("No payment method found with the given ID.");

                return Ok(paymentMethod);
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
        public async Task<IActionResult> CreatePaymentMethod([FromBody] PaymentMethod paymentMethod)
        {
            try
            {
                if (paymentMethod == null)
                    return BadRequest("Payment method is null.");
                bool isCreated  = await _paymentMethodService.CreatePaymentMethod(paymentMethod);
                if (!isCreated)
                    return StatusCode(500, "Failed to create the payment.");
                return CreatedAtAction(nameof(GetPaymenMethodById), new { id = paymentMethod.Id }, paymentMethod);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating payment method: {ex.Message}");
            }
        }
        
        [HttpDelete("{id}")]
        public IActionResult DeletePaymentMethod(int id)
        {
            bool deleted = _paymentMethodService.DeletePaymentMethod(id);

            if (deleted)
                return Ok(new { message = "Payment method deleted successfully." });
            else
                return NotFound(new { message = "No payment method found with the given ID." });
        }

    }
}