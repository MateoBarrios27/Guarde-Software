using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.address;
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Address;
using System.Threading.Tasks;


namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService) { 

            _addressService = addressService;
        }

        [HttpGet]
        public async Task<ActionResult<List<Address>>> GetAddresses()
        {
            try
            {
                List<Address> addresses = await _addressService.GetAddressList();

                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting addresses: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Address>> GetAddressById(int id)
        {
            try
            {
                List<Address> addresses = await _addressService.GetAddressListByClientId(id);

                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting addresses by client id: {ex.Message}");
            }
        }

        [HttpPut("{clientId}")]
        public async Task<ActionResult> UpdateAddress(int clientId, [FromBody] UpdateAddressDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest("Address data is required.");

                bool updated = await _addressService.UpdateAddress(clientId, dto);

                if (!updated)
                    return NotFound($"No address found with Id {dto.Id} for client {clientId}.");

                return Ok("Address updated successfully.");
            }   
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating address: {ex.Message}");
            }
        }
    }
}