namespace GuardeSoftwareAPI.Dtos.Locker
{
    // This DTO is used to return information that GetUserDetail.cs uses
    public class GetLockerClientDetailDTO
    {
        public int Id { get; set; }
        public string Warehouse { get; set; } = string.Empty;
        public string LockerType { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
    }
}