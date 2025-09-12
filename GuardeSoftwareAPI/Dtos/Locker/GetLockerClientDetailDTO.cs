namespace GuardeSoftwareAPI.Dtos.Locker
{
    // This DTO is used to return information that GetUserDetail.cs uses
    public class GetLockerClientDetailDTO
    {
        public string Warehouse { get; set; } = string.Empty;
        public string LockerType { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
    }
}