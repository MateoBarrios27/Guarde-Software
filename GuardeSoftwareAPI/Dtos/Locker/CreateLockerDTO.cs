namespace GuardeSoftwareAPI.Dtos.Locker
{
    public class CreateLockerDTO
    {
        public int WarehouseId { get; set; }
        public int LockerTypeId { get; set; }
        public string? Identifier { get; set; } = string.Empty;
        public string? Features { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsFreeSpace { get; set; } = false;
    }
}