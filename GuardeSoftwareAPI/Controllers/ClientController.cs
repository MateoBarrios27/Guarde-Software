using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<Client>> GetClients()
        {
            try
            {
                List<Client> clients = null; //replace with service call

                return Ok(clients);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting clients: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<Client> GetClientPerId(int id)
        {
            try
            {
                Client client = new Client(); //replace with service call

                if (client == null)
                {
                    return NotFound($"Client ID n°{id} not found ");
                }
                return Ok(client);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the client: {ex.Message}");
            }
        }        
    }
}