using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Cash;
using GuardeSoftwareAPI.Services.activityLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GuardeSoftwareAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/cashflow/receivables")]
public class CashReceivablesController : ControllerBase, IAsyncActionFilter
{
    private readonly DaoCashReceivables _dao;
    private readonly IActivityLogService _activityLogService;
    public CashReceivablesController(AccessDB db, IActivityLogService activityLogService)
    {
        _dao = new DaoCashReceivables(db);
        _activityLogService = activityLogService;
    }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!await _activityLogService.IsCurrentUserAdminAsync()) { context.Result = Forbid(); return; }
        await next();
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _dao.GetAsync());

    [HttpPost]
    public Task<IActionResult> Create(CashReceivableInput input) => Mutate(() => _dao.CreateAsync(input));

    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, CashReceivableInput input) => Mutate(() => _dao.UpdateAsync(id, input));

    [HttpDelete("{id:int}")]
    public Task<IActionResult> Delete(int id) => Mutate(() => _dao.DeleteAsync(id));

    [HttpPost("{id:int}/payments")]
    public Task<IActionResult> AddPayment(int id, CashReceivablePaymentInput input) => Mutate(() => _dao.AddPaymentAsync(id, input));

    [HttpDelete("{id:int}/payments/{paymentId:int}")]
    public Task<IActionResult> DeletePayment(int id, int paymentId) => Mutate(() => _dao.DeletePaymentAsync(id, paymentId));

    private async Task<IActionResult> Mutate(Func<Task<int>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
