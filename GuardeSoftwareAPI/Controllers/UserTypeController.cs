using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.UserType;
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
        public async Task<ActionResult<List<UserType>>> GetUserTypes()
        {
            try
            {
                List<UserType> userTypes = await _userTypeService.GetUserTypeList();

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

        [HttpGet("{id}", Name = "GetUserTypeById")]
        public async Task<IActionResult> GetUserTypeById(int id)
        {
            try
            {
                UserType userType = await _userTypeService.GetUserTypeById(id);
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

        [HttpPost]
        public async Task<IActionResult> CreateUserType([FromBody] CreateUserTypeDTO userTypeToCreate)
        {
            if (userTypeToCreate == null)
                return BadRequest("User type is null.");

            UserType userType = new()
            {
                Name = userTypeToCreate.Name
            };
            
            try
            {


                userType = await _userTypeService.CreateUserType(userType);

                if (userType == null)
                    return StatusCode(500, "A problem happened while handling your request.");
                if (userType.Id == 0)
                    return StatusCode(500, "A problem happened while handling your request. No ID was returned for the new user type.");

                return CreatedAtAction(nameof(GetUserTypeById), new { id = userType.Id }, userType);
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating user type: {ex.Message}");
            }
        }
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUserType(int id)
        {
            bool deleted = await _userTypeService.DeleteUserType(id);

            if (deleted)
                return Ok(new { message = "User deleted successfully." });
            else
                return NotFound(new { message = "No user found with the given ID." });
        }
    }
}
