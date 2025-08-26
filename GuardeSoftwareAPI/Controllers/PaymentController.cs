using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
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
