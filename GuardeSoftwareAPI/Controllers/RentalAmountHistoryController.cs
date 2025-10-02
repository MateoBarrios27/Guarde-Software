using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.RentalAmountHistoryDto;
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
        public async Task<ActionResult<List<RentalAmountHistory>>> GetRentalAmountHistories()
        {
            try
            {
                List<RentalAmountHistory> rentalAmountHistories = await _rentalAmountHistoryService.GetRentalAmountHistoriesList();

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

        [HttpGet("ByRentalId/{rentalId}")]
        public async Task<IActionResult> GetRentalAmountHistoryByRentalId(int rentalId)
        {
            try
            {
                RentalAmountHistory rentalAmountHistory = await _rentalAmountHistoryService.GetRentalAmountHistoryByRentalId(rentalId);
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

        [HttpPost]
        public async Task<IActionResult> CreateRentalAmountHistory([FromBody] CreateRentalAmountHistoryDto RentalAmountHistoryToCreate)
        {
            if (RentalAmountHistoryToCreate == null)
                return BadRequest("Rental amount history is null.");
                
            RentalAmountHistory rentalAmountHistory = new()
            {
                RentalId = RentalAmountHistoryToCreate.RentalId,
                Amount = RentalAmountHistoryToCreate.Amount,
                StartDate = RentalAmountHistoryToCreate.StartDate
            };

            try
            {
                rentalAmountHistory = await _rentalAmountHistoryService.CreateRentalAmountHistory(rentalAmountHistory);

                if (rentalAmountHistory == null || rentalAmountHistory.Id <= 0)
                    return StatusCode(500, "A problem happened while handling your request.");

                return CreatedAtAction(nameof(GetRentalAmountHistoryByRentalId), new { id = rentalAmountHistory.Id }, rentalAmountHistory);
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating rental amount history: {ex.Message}");
            }
        }
    }
}