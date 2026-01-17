using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.PaymentMethod;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.paymentMethod;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]

    public class PaymentMethodController : ControllerBase
    {
        private readonly IPaymentMethodService _paymentMethodService;

        public PaymentMethodController(IPaymentMethodService paymentMethodService)
        {
            _paymentMethodService = paymentMethodService;
        }

        [HttpGet]
        public async Task<ActionResult<List<PaymentMethod>>> GetPaymentMethods()
        {
            try
            {
                List<PaymentMethod> paymentMethods = await _paymentMethodService.GetPaymentMethodsList();

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

        [HttpGet("{id}", Name = "GetPaymenMethodById")]
        public async Task<IActionResult> GetPaymenMethodById(int id)
        {
            if (id <= 0) return BadRequest("Invalid payment method ID.");
            try
            {
                PaymentMethod paymentMethod = await _paymentMethodService.GetPaymentMethodById(id);
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
        public async Task<IActionResult> CreatePaymentMethod([FromBody] CreatePaymentMethodDto paymentMethodToCreate)
        {
            if (paymentMethodToCreate == null)
                return BadRequest("Payment method data is null.");

            PaymentMethod paymentMethod = new()
            {
                Name = paymentMethodToCreate.Name,
                Commission = paymentMethodToCreate.Commission ?? 0m // Default to 0 if null
            };

            try
            {
                paymentMethod = await _paymentMethodService.CreatePaymentMethod(paymentMethod);

                if (paymentMethod == null || paymentMethod.Id <= 0)
                    return StatusCode(500, "A problem happened while handling your request.");

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
        public async Task<IActionResult> DeletePaymentMethod(int id)
        {
            bool deleted = await _paymentMethodService.DeletePaymentMethod(id);

            if (deleted)
                return Ok(new { message = "Payment method deleted successfully." });
            else
                return NotFound(new { message = "No payment method found with the given ID." });
        }

        [HttpPatch("{paymentMethodId}")]
        public async Task<IActionResult> UpdatePaymentMethod(int paymentMethodId, [FromBody] UpdatePaymentMethodDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest("payment method data is required.");

                bool updated = await _paymentMethodService.UpdatePaymentMethod(paymentMethodId, dto);

                if (!updated)
                    return NotFound($"No payment method found with Id {paymentMethodId}");

                return Ok(new { message = "Payment Method updated successfully." });
            }   
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating payment method: {ex.Message}");
            }
        }

    }
}