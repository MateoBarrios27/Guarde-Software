using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.client;
using GuardeSoftwareAPI.Dtos.Client;
using Microsoft.AspNetCore.Mvc;

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
        public ActionResult<List<Client>> GetClients()
        {
            try
            {
                List<Client> clients = _clientService.GetClientsList();

                return Ok(clients);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting clients: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<Client> GetClientById(int id)
        {
            try
            {
                List<Client> client = _clientService.GetClientListById(id);

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
    }
}