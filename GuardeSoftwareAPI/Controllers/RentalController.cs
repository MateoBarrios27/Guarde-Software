using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.rental;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RentalController : ControllerBase
    {
        private readonly IRentalService _rentalService;

        public RentalController(IRentalService rentalService)
        {
            _rentalService = rentalService;
        }

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