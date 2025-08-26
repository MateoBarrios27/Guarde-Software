using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RentalController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<Rental>> GetRentals()
        {
            try
            {
                List<Rental> rentals = null; //replace with service call

                return Ok(rentals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting rentals: {ex.Message}");
            }
        }
    }
}