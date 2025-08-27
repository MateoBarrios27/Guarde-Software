using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.Address;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }
        
        [HttpGet]
        public ActionResult<List<Address>> GetAddresses()
        {
            try
            {
                List<Address> addresses = null; //replace with service call

                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting addresses: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<Address> GetAddressById(int id)
        {
            try
            {
                AccountMovement accountMovement = new AccountMovement(); //replace with service call

                if (accountMovement == null)
                {
                    return NotFound($"Account movement id n°{id} not found ");
                }
                return Ok(accountMovement);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting the account movement: {ex.Message}");
            }
        }    
    }
}