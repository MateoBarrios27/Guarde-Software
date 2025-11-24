using System.Collections.Generic;
using System.Security.Claims;

namespace GuardeSoftwareAPI.Auth
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(ApplicationUser user, IList<string> roles);
    }
}