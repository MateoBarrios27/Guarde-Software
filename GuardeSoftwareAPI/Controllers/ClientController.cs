using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.client;
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
        public ActionResult CreateClient([FromBody] Client client)
        {
            try
            {
                //call to service to create the client
                return CreatedAtAction(nameof(GetClientById), new { id = client.Id }, client);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading the client: {ex.Message}");
            }
        }    
    }
}