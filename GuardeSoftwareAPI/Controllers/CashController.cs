using GuardeSoftwareAPI.Dtos.Cash;
using GuardeSoftwareAPI.Services.cash;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    // [Authorize(Roles = "Admin")] // <--- CLAVE: Solo admins
    [Route("api/[controller]")]
    [ApiController]
    public class CashFlowController : ControllerBase
    {
        private readonly ICashService _service;

        public CashFlowController(ICashService service)
        {
            _service = service;
        }

        // GET: api/cashflow/items?month=8&year=2025
        [HttpGet("items")]
        public async Task<IActionResult> GetItems([FromQuery] int month, [FromQuery] int year)
        {
            var items = await _service.GetItemsAsync(month, year);
            return Ok(items);
        }

        // POST: api/cashflow/items (Sirve para Crear y Editar)
        [HttpPost("items")]
        public async Task<IActionResult> UpsertItem([FromBody] CashFlowItemDto item)
        {
            try
            {
                int id = await _service.UpsertItemAsync(item);
                return Ok(id);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error guardando item de caja", details = ex.Message });
            }
        }

        // DELETE: api/cashflow/items/5
        [HttpDelete("items/{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            await _service.DeleteItemAsync(id);
            return NoContent();
        }

        // GET: api/cashflow/summary?month=8&year=2025
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int month, [FromQuery] int year)
        {
            var summary = await _service.GetMonthlySummaryAsync(month, year);
            return Ok(summary);
        }

        // GET: api/cashflow/accounts
        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts()
        {
            var accounts = await _service.GetAccountsAsync();
            return Ok(accounts);
        }

        // PUT: api/cashflow/accounts/1
        [HttpPut("accounts/{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountRequest request)
        {
            await _service.UpdateAccountBalanceAsync(id, request.Balance);
            return NoContent();
        }
    }
}