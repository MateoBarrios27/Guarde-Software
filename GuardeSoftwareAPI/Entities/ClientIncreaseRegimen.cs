namespace GuardeSoftwareAPI.Entities
{
    public class ClientIncreaseRegimen
    {
        public int ClientId { get; set; }
        public int RegimenId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}