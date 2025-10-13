namespace GuardeSoftwareAPI.Dtos.User
{
    public class CreateUserDTO
    {
        public string UserName { get; set; } = string.Empty;
        public int UserTypeId {get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}