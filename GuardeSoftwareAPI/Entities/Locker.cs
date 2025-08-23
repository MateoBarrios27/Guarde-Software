namespace GuardeSoftwareAPI.Entities
{
    public class Locker
    {
        public int Id { get; set; }
        public int WarehouseId { get; set; } 
        public int LockerTypeId { get; set; } 
        public string Identifier { get; set; } = string.Empty; // e.g., 101, 303, etc.
        public string? Features { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // e.g., Available, Rented, Maintenance
    }
}