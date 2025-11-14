namespace GuardeSoftwareAPI.Dtos.RentalSpaceRequest
{
    public class SpaceRequestDTO
    {
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }
        public decimal M3 { get; set; }
    }
}