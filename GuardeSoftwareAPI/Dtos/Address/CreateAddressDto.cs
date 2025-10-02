namespace GuardeSoftwareAPI.Dtos.Address
{
    public class CreateAddressDto
    {
        public int ClientId { get; set; }
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
    }
}