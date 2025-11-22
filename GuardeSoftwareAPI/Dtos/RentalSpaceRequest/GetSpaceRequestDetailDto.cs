namespace GuardeSoftwareAPI.Dtos.RentalSpaceRequest
{
    public class GetSpaceRequestDetailDto
    {
        public string Warehouse { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal M3 { get; set; }
    }
}