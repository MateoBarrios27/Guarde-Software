using System;


namespace GuardeSoftwareAPI.Dtos.Email
{
    public class UpdateEmailDto
    {
        public int Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? Type { get; set; } = string.Empty;
    }
}
