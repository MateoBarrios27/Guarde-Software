using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Services.address;

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
        public ActionResult<List<Address>> GetAddresses()
        {
            try
            {
                List<Address> addresses = _addressService.GetAddressList();

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
                List<Address> addresses = _addressService.GetAddressListByClientId(id);

                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting addresses by client id: {ex.Message}");
            }
        }    
    }
}