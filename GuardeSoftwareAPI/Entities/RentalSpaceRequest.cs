namespace GuardeSoftwareAPI.Entities
{
    public class RentalSpaceRequest
    {
        public int Id { get; set; }
        public int RentalId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }
        public decimal M3 { get; set; }
        public string? Comment { get; set; }
    }
}