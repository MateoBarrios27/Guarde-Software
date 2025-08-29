namespace GuardeSoftwareAPI.Entities
{
    public class Rental
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? ContractedM3 { get; set; }
    }
}