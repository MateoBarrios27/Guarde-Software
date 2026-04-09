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

        [HttpGet("items")]
        public async Task<IActionResult> GetItems([FromQuery] int month, [FromQuery] int year)
        {
            var items = await _service.GetItemsAsync(month, year);
            return Ok(items);
        }

        [HttpPost("items")]
        public async Task<IActionResult> UpsertItem([FromBody] CashFlowItemDto item, [FromQuery] int month, [FromQuery] int year)
        {
            try
            {
                int id = await _service.UpsertItemAsync(item, month, year);
                return Ok(id);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error guardando item de caja", details = ex.Message });
            }
        }

        [HttpDelete("items/{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            await _service.DeleteItemAsync(id);
            return NoContent();
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int month, [FromQuery] int year)
        {
            var summary = await _service.GetMonthlySummaryAsync(month, year);
            return Ok(summary);
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts([FromQuery] int month, [FromQuery] int year)
        {
            var accounts = await _service.GetAccountsAsync(month, year);
            return Ok(accounts);
        }

        [HttpPut("accounts/{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountRequest request)
        {
            await _service.UpdateAccountBalanceAsync(id, request.Balance);
            return NoContent();
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> CreateAccount([FromBody] FinancialAccountDto account, [FromQuery] int month, [FromQuery] int year)
        {
            int id = await _service.CreateAccountAsync(account, month, year);
            return Ok(id);
        }

        [HttpDelete("accounts/{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            await _service.DeleteAccountAsync(id);
            return NoContent();
        }

        [HttpPost("update-order")]
        public async Task<IActionResult> UpdateOrder([FromBody] List<CashItemOrderDto> itemsOrder)
        {
            if (itemsOrder == null || itemsOrder.Count == 0)
                return BadRequest("La lista de ordenamiento está vacía.");

            try
            {
                await _service.UpdateItemsOrderAsync(itemsOrder);
                return Ok(new { message = "Orden actualizado correctamente" });
            }
            catch (Exception ex)
            {
                // Registrá el error real en tus logs
                return StatusCode(500, "Ocurrió un error interno al guardar el orden.");
            }
        }

        [HttpPost("update-accounts-order")]
        public async Task<IActionResult> UpdateAccountOrder([FromBody] List<AccountOrderDto> accountsOrder)
        {
            if (accountsOrder == null || accountsOrder.Count == 0)
                return BadRequest("La lista de ordenamiento está vacía.");

            try
            {
                await _service.UpdateAccountsOrderAsync(accountsOrder);
                return Ok(new { message = "Orden de cuentas actualizado correctamente" });
            }
            catch (Exception ex)
            {
                // Registrá el error real en tus logs
                return StatusCode(500, "Ocurrió un error interno al guardar el orden de cuentas.");
            }
        }
    }
}