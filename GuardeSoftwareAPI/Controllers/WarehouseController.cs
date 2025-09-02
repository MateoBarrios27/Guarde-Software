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
        public ActionResult<List<Warehouse>> GetUserWarehouses()
        {
            try
            {
                List<Warehouse> warehouses = _warehouseService.GetWarehouseList();

                return Ok(warehouses);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting warehouses: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetWarehouseById(int id)
        {
            try
            {
                Warehouse warehouse = _warehouseService.GetWarehouseById(id);
                if (warehouse == null)
                    return NotFound("No warehouse found with the given ID.");

                return Ok(warehouse);
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
        public IActionResult CreateWarehouse([FromBody] Warehouse warehouse)
        {
            try
            {
                if (warehouse == null)
                    return BadRequest("Warehouse is null.");
                bool isCreated = _warehouseService.CreateWarehouse(warehouse);
                if (!isCreated)
                    return StatusCode(500, "Failed to create the warehouse.");
                return CreatedAtAction(nameof(GetWarehouseById), new { id = warehouse.Id }, warehouse);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating the payment: {ex.Message}");
            }
        }
        
        [HttpDelete("{id}")]
        public IActionResult DeleteWarehouse(int id)
        {
            bool deleted = _warehouseService.DeleteWarehouse(id);

            if (deleted)
                return Ok(new { message = "Warehouse deleted successfully." });
            else
                return NotFound(new { message = "No warehouse found with the given ID." });
        }
    }
}
