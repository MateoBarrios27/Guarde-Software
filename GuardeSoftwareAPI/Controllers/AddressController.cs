using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.address;
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dtos.Address;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;


namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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

        [HttpGet("ByClientId/{id}", Name = "GetAddressByClientId")]
        public async Task<ActionResult<Address>> GetAddressByClientId(int id)
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

        [HttpPost]
        public async Task<ActionResult<int>> CreateAddress([FromBody] CreateAddressDto addressToCreate)
        {
            if (addressToCreate == null)
                    return BadRequest("Address data is required.");

            Address address = new()
            {
                ClientId = addressToCreate.ClientId,
                Street = addressToCreate.Street,
                City = addressToCreate.City,
                Province = addressToCreate.Province
            };

            try
            {
                address = await _addressService.CreateAddress(address);

                return CreatedAtAction(nameof(GetAddressByClientId), new { id = address.Id }, address);
            }
                catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating address: {ex.Message}");
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