using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.userType;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserTypeController : ControllerBase
    {
        private readonly IUserTypeService _userTypeService;

        public UserTypeController(IUserTypeService userTypeService)
        {
            _userTypeService = userTypeService;
        }

        [HttpGet]
        public ActionResult<List<UserType>> GetUserTypes()
        {
            try
            {
                List<UserType> userTypes = _userTypeService.GetUserTypeList();

                return Ok(userTypes);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting user types: {ex.Message}");
            }
        }
        
        [HttpGet("{id}")]
        public IActionResult GetUserTypeById(int id)
        {
            try
            {
                UserType userType = _userTypeService.GetUserTypeById(id);
                if (userType == null)
                    return NotFound("No user type found with the given ID.");

                return Ok(userType);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
