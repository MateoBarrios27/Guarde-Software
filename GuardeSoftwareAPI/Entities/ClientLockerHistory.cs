namespace GuardeSoftwareAPI.Entities
{
    public class ClientLockerHistory
    {
        public int Id { get; set; }
        public string LockerIdentifier { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string LockerType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}