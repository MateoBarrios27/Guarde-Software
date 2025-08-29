using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RentalAmountHistoryController : ControllerBase
    {
        private readonly IRentalAmountHistoryService _rentalAmountHistoryService;

        public RentalAmountHistoryController(IRentalAmountHistoryService rentalAmountHistoryService)
        {
            _rentalAmountHistoryService = rentalAmountHistoryService;
        }

        [HttpGet]
        public ActionResult<List<RentalAmountHistory>> GetRentalAmountHistories()
        {
            try
            {
                List<RentalAmountHistory> rentalAmountHistories = _rentalAmountHistoryService.GetRentalAmountHistoriesList();

                return Ok(rentalAmountHistories);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting rental amount histories: {ex.Message}");
            }
        }
        
        [HttpGet("ByRental/{rentalId}")]
        public IActionResult GetRentalAmountHistoryByRentalId(int rentalId)
        {
            try
            {
                RentalAmountHistory rentalAmountHistory = _rentalAmountHistoryService.GetRentalAmountHistoryByRentalId(rentalId);
                if (rentalAmountHistory == null)
                    return NotFound("No rental amount history found with the given ID.");

                return Ok(rentalAmountHistory);
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