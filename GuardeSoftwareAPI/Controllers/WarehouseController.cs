using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.warehouse;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WarehouseController : ControllerBase
    {
        private readonly IWarehouseService _warehouseService;

        public WarehouseController(IWarehouseService warehouseService)
        {
            _warehouseService = warehouseService;
        }

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
