using GuardeSoftwareAPI.Dtos.Chat;
using GuardeSoftwareAPI.Services.chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GuardeSoftwareAPI.Controllers
{
    /// <summary>
    /// API pública del widget de la web. No usa JWT: cada conversación se identifica por
    /// su id público y un token que sólo conoce el navegador del visitante. El CORS de
    /// esta API es una política aparte, limitada a los dominios de la web.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [EnableCors(PublicChatPolicy.Name)]
    [Route("api/public/chat")]
    public sealed class PublicChatController : ChatControllerBase
    {
        public const string TokenHeader = "X-Chat-Visitor-Token";

        private readonly IChatService _chat;

        public PublicChatController(IChatService chat)
        {
            _chat = chat;
        }

        [HttpGet("status")]
        [EnableRateLimiting(PublicChatPolicy.ReadLimiter)]
        public Task<IActionResult> GetStatus() => RunAsync(async () =>
        {
            NoStore();
            return Ok(await _chat.GetStatusAsync());
        });

        [HttpPost("conversations")]
        [EnableRateLimiting(PublicChatPolicy.StartLimiter)]
        public Task<IActionResult> Start([FromBody] StartChatRequest request) => RunAsync(async () =>
        {
            NoStore();
            return Ok(await _chat.StartConversationAsync(request));
        });

        [HttpGet("conversations/{publicId:guid}")]
        [EnableRateLimiting(PublicChatPolicy.ReadLimiter)]
        public Task<IActionResult> Get(Guid publicId, [FromQuery] long after = 0) => RunAsync(async () =>
        {
            NoStore();
            return Ok(await _chat.GetVisitorConversationAsync(publicId, VisitorToken(), after));
        });

        [HttpPost("conversations/{publicId:guid}/messages")]
        [EnableRateLimiting(PublicChatPolicy.WriteLimiter)]
        public Task<IActionResult> Send(Guid publicId, [FromBody] SendChatMessageRequest request) => RunAsync(async () =>
        {
            NoStore();
            return Ok(await _chat.SendVisitorMessageAsync(publicId, VisitorToken(), request.Body));
        });

        [HttpPost("conversations/{publicId:guid}/read")]
        [EnableRateLimiting(PublicChatPolicy.WriteLimiter)]
        public Task<IActionResult> MarkRead(Guid publicId, [FromBody] MarkChatReadRequest? request) => RunAsync(async () =>
        {
            NoStore();
            await _chat.MarkVisitorReadAsync(publicId, VisitorToken(), request?.UpToMessageId);
            return NoContent();
        });

        private string? VisitorToken()
            => Request.Headers.TryGetValue(TokenHeader, out var values) ? values.ToString() : null;

        // Lo que el visitante ve cambia a cada rato: ningún proxy debe guardarlo.
        private void NoStore() => Response.Headers.CacheControl = "no-store";
    }
}
