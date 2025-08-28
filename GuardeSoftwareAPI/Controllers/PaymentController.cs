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
                List<Payment> payments = null; //replace with service call

                return Ok(payments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting payments: {ex.Message}");
            }
        }
    }
}
