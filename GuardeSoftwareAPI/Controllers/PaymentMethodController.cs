using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentMethodController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<PaymentMethod>> GetPaymentMethods()
        {
            try
            {
                List<PaymentMethod> paymentMethods = null; //replace with service call

                return Ok(paymentMethods);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting payment methods: {ex.Message}");
            }
        }
    }
}