using GuardeSoftwareAPI.Auth.Dto;
using GuardeSoftwareAPI.Services.auth;
using Microsoft.AspNetCore.Mvc;

namespace GuardeSoftwareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            var response = await _authService.RegisterAsync(dto);
            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            try
            {
                var auth = await _authService.LoginAsync(dto);

                if (auth == null)
                {
                    return Unauthorized("Credenciales inválidas");
                }

                return Ok(auth);
            }
            catch
            {
                return StatusCode(500, "Error interno en el login");
            }
        }
    }
}
