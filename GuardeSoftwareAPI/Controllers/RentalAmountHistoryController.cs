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
                List<RentalAmountHistory> rentalAmountHistories = null; //replace with service call

                return Ok(rentalAmountHistories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting rental amount histories: {ex.Message}");
            }
        }
    }
}