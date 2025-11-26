using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.client;
using GuardeSoftwareAPI.Dtos.Client;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController : ControllerBase
    {
        private readonly IClientService _clientService;

        public ClientController(IClientService clientService)
        {
            _clientService = clientService;
        }

        [HttpGet]
        public async Task<ActionResult<List<Client>>> GetClients()
        {
            try
            {
                List<Client> clients = await _clientService.GetClientsList();

                return Ok(clients);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting clients: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Client>> GetClientById(int id)
        {
            try
            {
                List<Client> client = await _clientService.GetClientListById(id);

                if (client == null)
                {
                    return NotFound($"Client id n°{id} not found ");
                }
                return Ok(client);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the client: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Client>> CreateClient([FromBody] CreateClientDTO dto)
        {
            try
            {
                int newId = await _clientService.CreateClientAsync(dto);

                return CreatedAtAction(nameof(GetClientById), new { id = newId }, newId);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading the client: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClient(int id, [FromBody] CreateClientDTO dto)
        {
            // Validar que el ID de la ruta coincida si el DTO tiene ID (opcional pero bueno)
            if (dto.Id.HasValue && dto.Id.Value != id)
            {
                return BadRequest(new { message = "El ID en la ruta no coincide con el ID en el cuerpo de la solicitud." });
            }

            try
            {
                bool success = await _clientService.UpdateClientAsync(id, dto);

                if (success)
                {
                    return NoContent(); // Respuesta estándar para un PUT exitoso sin contenido que devolver
                }
                else
                {
                    // Si el servicio devuelve false, asumimos que no encontró el cliente
                    return NotFound(new { message = $"Cliente con ID {id} no encontrado." });
                }
            }
            catch (InvalidOperationException ex) // Captura excepciones específicas (ej: DNI duplicado en otro cliente)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex) // Captura errores de validación
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Log the exception details here
                return StatusCode(500, new { message = $"Error al actualizar el cliente: {ex.Message}" });
            }
        }

        [HttpGet("detail/{id}")]
        public async Task<ActionResult<GetClientDetailDTO>> GetClientDetailById(int id)
        {
            try
            {
                GetClientDetailDTO clientDetail = await _clientService.GetClientDetailByIdAsync(id);

                if (clientDetail == null)
                {
                    return NotFound($"Client id n°{id} not found ");
                }
                return Ok(clientDetail);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the client detail: {ex.Message}");
            }
        }

        [HttpGet("table")]
        public async Task<IActionResult> GetClients([FromQuery] GetClientsRequestDto request)
        {
            var result = await _clientService.GetClientsTableAsync(request);
            return Ok(result);
        }

        [HttpGet("recipient-options")]
        public async Task<IActionResult> GetRecipientOptions()
        {
            try
            {
                // We'll create this service method next
                var names = await _clientService.GetClientRecipientNamesAsync();

                // Add the hardcoded groups
                var options = new List<string>
                {
                    "Todos los clientes",
                    "Clientes morosos",
                    "Clientes al día"
                };

                options.AddRange(names);
                return Ok(options);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
        
        [HttpGet("search")]
        public async Task<IActionResult> SearchClients([FromQuery] string query)
        {
            try
            {
                var names = await _clientService.SearchClientNamesAsync(query);
                return Ok(names);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeactivateClient(int id)
        {
            try
            {
                await _clientService.DeactivateClientAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex) 
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}