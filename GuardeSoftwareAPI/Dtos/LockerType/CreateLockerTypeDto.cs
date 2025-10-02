namespace GuardeSoftwareAPI.Dtos.LockerType
{
    public class CreateLockerTypeDto
    {
        public string Name { get; set; } = string.Empty; // e.g., Box, Locker, free space, etc.
        public decimal Amount { get; set; }
        public decimal? M3 { get; set; }
    }
}