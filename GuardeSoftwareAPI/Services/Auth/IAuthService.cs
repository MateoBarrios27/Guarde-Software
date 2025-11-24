using GuardeSoftwareAPI.Auth.Dto;

namespace GuardeSoftwareAPI.Services.auth
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto);
        Task<AuthResponseDto> LoginAsync(LoginRequestDto dto);
    }
}
