using GuardeSoftwareAPI.Services.phone;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers.phone
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PhoneController : ControllerBase
    {
        private readonly IPhoneService _phoneService;

        public PhoneController(IPhoneService phoneService)
        {
            _phoneService = phoneService;
        }

        [HttpGet("GetPhones")]
        public async Task<IActionResult> GetPhones()
        {
            try
            {
                var phones = await _phoneService.GetPhonesList();
                return Ok(phones);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("GetPhonesByClientId/{id}")]
        public async Task<IActionResult> GetPhonesByClientId(int id)
        {
            try
            {
                var phones = await _phoneService.GetPhoneListByClientId(id);
                return Ok(phones);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}