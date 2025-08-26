using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WarehouseController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<Warehouse>> GetWarehouses()
        {
            try
            {
                List<Warehouse> warehouses = null; //replace with service call

                return Ok(warehouses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting warehouses: {ex.Message}");
            }
        }
    }
}
