using GuardeSoftwareAPI.Dtos.Alert;
using GuardeSoftwareAPI.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using GuardeSoftwareAPI.Services.notification;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlertController : ControllerBase
    {
        private readonly IHubContext<AlertHub> _hubContext;
        private readonly ILogger<AlertController> _logger;
        private readonly INotificationService _notificationService;

        // Almacenamiento en memoria del último cartel activo
        // (en una implementación más robusta, esto podría ir a DB o cache distribuido)
        private static SystemAlertDto? _activeAlert = null;

        public AlertController(
            IHubContext<AlertHub> hubContext,
            ILogger<AlertController> logger,
            INotificationService notificationService)
        {
            _hubContext = hubContext;
            _logger = logger;
            _notificationService = notificationService;
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
                          ?? User.FindFirst("username")?.Value
                          ?? "Administrador";

            dto.SenderName = senderName;
            dto.CreatedAt = DateTime.Now;

            int? senderUserId = int.TryParse(User.FindFirst("businessUserId")?.Value, out int parsedUserId)
                ? parsedUserId
                : null;

            await _notificationService.CreateEventAsync(
                sourceType: "system_alert",
                severity: NormalizeNotificationSeverity(dto.Severity),
                title: dto.Title.Trim(),
                message: dto.Message.Trim(),
                actionUrl: null,
                createdByUserId: senderUserId);

            // Guardar como alerta activa
            _activeAlert = dto;

            // Emitir a todos los clientes conectados
            await _hubContext.Clients.All.SendAsync("ReceiveSystemAlert", dto);

            _logger.LogInformation(
                "Alerta del sistema emitida por {Sender}: [{Severity}] {Title}",
                senderName, dto.Severity, dto.Title
            );

            return Ok(new { message = "Alerta enviada y guardada en la bandeja de notificaciones." });
        }

        private static string NormalizeNotificationSeverity(string severity)
            => severity?.Trim().ToLowerInvariant() switch
            {
                "danger" => "danger",
                "warning" => "warning",
                "maintenance" => "warning",
                _ => "info"
            };

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
