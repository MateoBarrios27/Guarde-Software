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
                List<Rental> rentals = _rentalService.GetRentalsList();

                return Ok(rentals);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting rentals: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetRentalById(int id)
        {
            try
            {
                Rental rental = _rentalService.GetRentalById(id);
                if (rental == null)
                    return NotFound("No rental found with the given ID.");

                return Ok(rental);
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