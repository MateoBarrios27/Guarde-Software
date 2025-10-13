namespace GuardeSoftwareAPI.Dtos.User
{
    public class UpdateUserDTO
    {
        public int UserTypeId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}