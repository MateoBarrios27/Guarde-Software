using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.Warehouse;
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
        public async Task<ActionResult<List<Warehouse>>> GetUserWarehouses()
        {
            try
            {
                List<Warehouse> warehouses = await _warehouseService.GetWarehouseList();

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

        [HttpGet("{id}", Name = "GetWarehouseById")]
        public async Task<IActionResult> GetWarehouseById(int id)
        {
            try
            {
                Warehouse warehouse = await _warehouseService.GetWarehouseById(id);
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
        public async Task<IActionResult> Create([FromBody] CreateWarehouseDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try {
                var result = await _warehouseService.CreateWarehouseAsync(dto);
                return Ok(result);
            } catch (Exception ex) {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateWarehouseDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try {
                var result = await _warehouseService.UpdateWarehouseAsync(id, dto);
                if (!result) return NotFound();
                return NoContent();
            } catch (Exception ex) {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try {
                var result = await _warehouseService.DeleteWarehouseAsync(id);
                if (!result) return NotFound();
                return NoContent();
            } catch (InvalidOperationException ex) {
                return BadRequest(new { message = ex.Message });
            } catch (Exception ex) {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
