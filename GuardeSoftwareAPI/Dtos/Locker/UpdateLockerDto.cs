using System;


namespace GuardeSoftwareAPI.Dtos.Locker
{
    public class UpdateLockerDto
    {     
        public string? Identifier { get; set; } = string.Empty;
        public string? Features { get; set; } = string.Empty;
        public string? Status { get; set; } = string.Empty;
    }
}