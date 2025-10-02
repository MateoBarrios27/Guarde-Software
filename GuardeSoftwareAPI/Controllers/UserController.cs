using GuardeSoftwareAPI.Dtos.User;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.user;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<List<GetUserDTO>>> GetUsers()
        {
            try
            {
                List<User> users = await _userService.GetUserList();

                List<GetUserDTO> usersList = users.Select(user => new GetUserDTO
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    UserTypeId = user.UserTypeId
                }).ToList();

                return Ok(usersList);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting users: {ex.Message}");
            }
        }

        [HttpGet("{id}", Name = "GetUserById")]
        public async Task<ActionResult<GetUserDTO>> GetUserById(int id)
        {
            User userFromService = await _userService.GetUserById(id);
            if (userFromService == null)
            {
                return NotFound();
            }

            GetUserDTO userDto = new GetUserDTO
            {
                Id = userFromService.Id,
                UserName = userFromService.UserName,
                FirstName = userFromService.FirstName ?? string.Empty,
                LastName = userFromService.LastName ?? string.Empty,
                UserTypeId = userFromService.UserTypeId
            };

            return Ok(userDto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO userToCreate)
        {
            if (userToCreate == null)
                return BadRequest("User data is required.");
            try
            {
                // Mapping from CreateUserDTO to User entity
            User user = new()
            {
                UserName = userToCreate.UserName,
                FirstName = userToCreate.FirstName,
                LastName = userToCreate.LastName,
                UserTypeId = 2, // Default to employee user
                PasswordHash = string.Empty // Password will be handled separately
            };

            // Send the user entity and password to the service
            User createdUser = await _userService.CreateUser(user, userToCreate.Password);

            if (createdUser == null)
            {
                return BadRequest("Failed to create user.");
            }
            
            // Mapping from User entity to GetUserDTO
            GetUserDTO userToReturn = new()
            {
                Id = createdUser.Id,
                UserName = createdUser.UserName,
                FirstName = createdUser.FirstName ?? string.Empty,
                LastName = createdUser.LastName ?? string.Empty,
                UserTypeId = createdUser.UserTypeId
            };

            return CreatedAtAction(nameof(GetUserById), new { id = userToReturn.Id }, userToReturn);
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating user: {ex.Message}");
            }
        }
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            bool deleted = await _userService.DeleteUser(id);

            if (deleted)
                return Ok(new { message = "User deleted successfully." });
            else
                return NotFound(new { message = "No user found with the given ID." });
        }
    }
}