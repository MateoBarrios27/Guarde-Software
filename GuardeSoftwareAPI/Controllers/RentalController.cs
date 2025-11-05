using System.Threading.Tasks;
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
        public async Task<ActionResult<List<Rental>>> GetRentals()
        {
            try
            {
                List<Rental> rentals = await _rentalService.GetRentalsList();

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
        public async Task<IActionResult> GetRentalById(int id)
        {
            try
            {
                Rental rental = await _rentalService.GetRentalById(id);
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

        [HttpGet("ByClientId/{Clientid}")]
        public async Task<IActionResult> GetRentalByClientId(int Clientid)
        {
            try
            {
                Rental rental = await _rentalService.GetRentalByClientId(Clientid);
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

         [HttpGet("Pending")]
        public async Task<ActionResult<List<PendingRentalDTO>>> GetPendingPayments()
        {
            try
            {
                List<PendingRentalDTO> rentals = await _rentalService.GetPendingPaymentsAsync();

                return Ok(rentals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting pending rentals: {ex.Message}");
            }
        }
        

        //[HttpPost]
        //public IActionResult CreateRental([FromBody] Rental rental)
        //{
        //    try
        //    {
        //        if (rental == null)
        //            return BadRequest("Rental is null.");
        //        bool isCreated = _rentalService.CreateRental(rental);
        //        if (!isCreated)
        //            return StatusCode(500, "Failed to create the rental.");
        //        return CreatedAtAction(nameof(GetRentalById), new { id = rental.Id }, rental);
        //    }
        //    catch (ArgumentException argEx)
        //    {
        //        return BadRequest(argEx.Message);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Error creating rental: {ex.Message}");
        //    }   
        //}

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRental(int id)
        {
            bool deleted = await _rentalService.DeleteRental(id);

            if (deleted)
                return Ok(new { message = "Rental deleted successfully." });
            else
                return NotFound(new { message = "No rental found with the given ID." });
        }
    }
}