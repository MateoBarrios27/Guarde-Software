using System;

namespace GuardeSoftwareAPI.Dtos.Address
{
    public class UpdateAddressDto
    {
        public int Id { get; set; }
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? Province { get; set; }
    }
}