namespace GuardeSoftwareAPI.Auth.Dto
{
    public class RegisterRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public int UserTypeId { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
