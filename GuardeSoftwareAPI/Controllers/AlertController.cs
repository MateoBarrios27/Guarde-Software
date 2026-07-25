using GuardeSoftwareAPI.Dtos.Alert;
using GuardeSoftwareAPI.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlertController : ControllerBase
    {
        private readonly IHubContext<AlertHub> _hubContext;
        private readonly ILogger<AlertController> _logger;

        // Almacenamiento en memoria del último cartel activo
        // (en una implementación más robusta, esto podría ir a DB o cache distribuido)
        private static SystemAlertDto? _activeAlert = null;

        public AlertController(IHubContext<AlertHub> hubContext, ILogger<AlertController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Emite un cartel de advertencia a todos los usuarios conectados en tiempo real.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendAlert([FromBody] SystemAlertDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "El título y el mensaje son requeridos." });

            // Enriquecer con datos del usuario que emite la alerta
            var senderName = User.FindFirst(ClaimTypes.Name)?.Value
                          ?? User.FindFirst("unique_name")?.Value
                          ?? "Administrador";

            dto.SenderName = senderName;
            dto.CreatedAt = DateTime.Now;

            // Guardar como alerta activa
            _activeAlert = dto;

            // Emitir a todos los clientes conectados
            await _hubContext.Clients.All.SendAsync("ReceiveSystemAlert", dto);

            _logger.LogInformation(
                "Alerta del sistema emitida por {Sender}: [{Severity}] {Title}",
                senderName, dto.Severity, dto.Title
            );

            return Ok(new { message = "Alerta enviada a todos los usuarios conectados." });
        }

        /// <summary>
        /// Retorna la alerta activa actual (útil para usuarios que se conectan mientras hay una alerta vigente).
        /// </summary>
        [HttpGet("active")]
        public IActionResult GetActiveAlert()
        {
            if (_activeAlert == null)
                return NoContent();

            return Ok(_activeAlert);
        }

        /// <summary>
        /// Limpia la alerta activa del sistema.
        /// </summary>
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearAlert()
        {
            _activeAlert = null;
            // Notificar a todos los clientes que la alerta fue levantada
            await _hubContext.Clients.All.SendAsync("ClearSystemAlert");
            return Ok(new { message = "Alerta del sistema limpiada." });
        }
    }
}
