namespace GuardeSoftwareAPI.Entities
{
    public class User
    {
        public int Id { get; set; }
        public int UserTypeId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }
}