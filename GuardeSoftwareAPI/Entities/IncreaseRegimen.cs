namespace GuardeSoftwareAPI.Entities
{
    public class IncreaseRegimen
    {
        public int Id { get; set; }
        public int Frequency { get; set; } // In months
        public decimal Percentage { get; set; }
    }
}