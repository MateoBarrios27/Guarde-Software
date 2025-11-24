using GuardeSoftwareAPI.Auth;
using GuardeSoftwareAPI.Auth.Dto;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GuardeSoftwareAPI.Services.auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly JwtOptions _jwtOptions;

        private readonly DaoUser _daoUser;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenGenerator jwtTokenGenerator,
            IOptions<JwtOptions> jwtOptions,
            DaoUser daoUser)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtTokenGenerator = jwtTokenGenerator;
            _jwtOptions = jwtOptions.Value;
            _daoUser = daoUser;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto)
        {       
            // 1) Crear usuario Identity (seguridad)
            var identityUser = new ApplicationUser
            {
                Email = dto.Email,
                UserName = dto.UserName
            };

            var result = await _userManager.CreateAsync(identityUser, dto.Password);

            if (!result.Succeeded)
                throw new Exception(string.Join(" | ", result.Errors.Select(e => e.Description)));

            // 2) Crear tu usuario negocio linkeado
            var businessUser = new User
            {
                UserTypeId = dto.UserTypeId,
                UserName = dto.UserName,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                IdentityUserId = identityUser.Id
            };

            await _daoUser.CreateUser(businessUser);

            // 3) (Opcional) mapear UserTypeId a Role Identity
            // Si querés:
            // var role = dto.UserTypeId == 1 ? "Admin" : "Employee";
            // await _userManager.AddToRoleAsync(identityUser, role);

            // 4) JWT
            var roles = await _userManager.GetRolesAsync(identityUser);
            var token = _jwtTokenGenerator.GenerateToken(identityUser, roles);

            return new AuthResponseDto
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiresInMinutes)
            };
        }       


        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto)
        {
            var identityUser =
                await _userManager.FindByEmailAsync(dto.EmailOrUserName)
                ?? await _userManager.FindByNameAsync(dto.EmailOrUserName);

            if (identityUser == null)
                throw new Exception("Credenciales inválidas");

            var check = await _signInManager.CheckPasswordSignInAsync(identityUser, dto.Password, false);
            if (!check.Succeeded)
                throw new Exception("Credenciales inválidas");

            var table = await _daoUser.GetUserByIdentityUserId(identityUser.Id);
            if (table.Rows.Count == 0)
                throw new Exception("Usuario de negocio no vinculado.");

            var row = table.Rows[0];
            var businessUser = new User
            {
                Id = Convert.ToInt32(row["user_id"]),
                UserTypeId = Convert.ToInt32(row["user_type_id"]),
                UserName = row["username"].ToString()!,
                FirstName = row["first_name"].ToString(),
                LastName = row["last_name"].ToString(),
                IdentityUserId = row["identity_user_id"].ToString()
            };

            var roles = await _userManager.GetRolesAsync(identityUser);
            var token = _jwtTokenGenerator.GenerateToken(identityUser, roles);

            return new AuthResponseDto
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiresInMinutes),

                // datos negocio para el front
                UserId = businessUser.Id,
                UserTypeId = businessUser.UserTypeId,
                UserName = businessUser.UserName,
                FirstName = businessUser.FirstName ?? "",
                LastName  = businessUser.LastName  ?? "",
            };
        }

    }
}
