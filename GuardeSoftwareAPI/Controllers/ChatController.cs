using System.Data;
using System.Security.Claims;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Chat;
using GuardeSoftwareAPI.Services.chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    /// <summary>
    /// Bandeja de la administración. Cualquier usuario autenticado puede leer; el
    /// middleware de Observador rechaza toda escritura, así que responder, cerrar y
    /// marcar como leído quedan reservados a Admin y operadores.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public sealed class ChatController : ChatControllerBase
    {
        private const string AdminUserTypeId = "1";

        private readonly IChatService _chat;
        private readonly DaoUser _daoUser;

        public ChatController(IChatService chat, DaoUser daoUser)
        {
            _chat = chat;
            _daoUser = daoUser;
        }

        [HttpGet("conversations")]
        public Task<IActionResult> GetConversations([FromQuery] string? status, [FromQuery] string? search)
            => RunAsync(async () => Ok(await _chat.GetConversationsAsync(status, search)));

        [HttpGet("conversations/{conversationId:long}")]
        public Task<IActionResult> GetConversation(long conversationId)
            => RunAsync(async () => Ok(await _chat.GetConversationAsync(conversationId)));

        [HttpGet("summary")]
        public Task<IActionResult> GetSummary()
            => RunAsync(async () => Ok(await _chat.GetInboxSummaryAsync()));

        [HttpPost("conversations/{conversationId:long}/read")]
        public Task<IActionResult> MarkRead(long conversationId, [FromBody] MarkChatReadRequest? request)
            => RunAsync(async () =>
            {
                await _chat.MarkReadAsync(conversationId, request?.UpToMessageId);
                return NoContent();
            });

        [HttpPost("conversations/{conversationId:long}/messages")]
        public Task<IActionResult> Reply(long conversationId, [FromBody] SendChatMessageRequest request)
            => RunAsync(async () =>
            {
                int? userId = await GetCurrentUserIdAsync();
                if (!userId.HasValue) return Unauthorized();

                // Sin RequestAborted: el mensaje ya se guardó y el estado del email debe quedar registrado
                // aunque el operador cierre la pestaña. El envío tiene su propio tiempo máximo.
                return Ok(await _chat.ReplyAsync(conversationId, userId.Value, request.Body));
            });

        [HttpPost("conversations/{conversationId:long}/close")]
        public Task<IActionResult> Close(long conversationId)
            => RunAsync(async () =>
            {
                int? userId = await GetCurrentUserIdAsync();
                if (!userId.HasValue) return Unauthorized();

                return Ok(await _chat.CloseAsync(conversationId, userId.Value));
            });

        [HttpPost("conversations/{conversationId:long}/reopen")]
        public Task<IActionResult> Reopen(long conversationId)
            => RunAsync(async () => Ok(await _chat.ReopenAsync(conversationId)));

        [HttpGet("business-hours")]
        public Task<IActionResult> GetBusinessHours()
            => RunAsync(async () => Ok(await _chat.GetBusinessHoursAsync()));

        [HttpPut("business-hours")]
        public Task<IActionResult> UpdateBusinessHours([FromBody] UpdateChatBusinessHoursRequest request)
            => RunAsync(async () =>
            {
                // Mismo criterio que el frontend: tipo de usuario 1 = Admin.
                if (User.FindFirst("businessUserTypeId")?.Value != AdminUserTypeId)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Sólo un administrador puede cambiar el horario del chat." });

                int? userId = await GetCurrentUserIdAsync();
                if (!userId.HasValue) return Unauthorized();

                return Ok(await _chat.UpdateBusinessHoursAsync(request, userId.Value));
            });

        private async Task<int?> GetCurrentUserIdAsync()
        {
            string? businessUserId = User.FindFirst("businessUserId")?.Value;
            if (int.TryParse(businessUserId, out int parsedId) && parsedId > 0)
                return parsedId;

            string? identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(identityUserId))
                return null;

            DataTable table = await _daoUser.GetUserByIdentityUserId(identityUserId);
            return table.Rows.Count == 0 ? null : Convert.ToInt32(table.Rows[0]["user_id"]);
        }
    }
}
