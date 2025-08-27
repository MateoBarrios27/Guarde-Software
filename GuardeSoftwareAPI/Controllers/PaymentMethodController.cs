using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.PaymentMethod;
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