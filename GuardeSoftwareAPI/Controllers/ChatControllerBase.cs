using GuardeSoftwareAPI.Services.chat;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    /// <summary>Traduce las excepciones del chat a respuestas HTTP con un mensaje apto para mostrar.</summary>
    public abstract class ChatControllerBase : ControllerBase
    {
        protected async Task<IActionResult> RunAsync(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (ChatValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ChatNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ChatConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ChatRateLimitException ex)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
            }
        }
    }
}
