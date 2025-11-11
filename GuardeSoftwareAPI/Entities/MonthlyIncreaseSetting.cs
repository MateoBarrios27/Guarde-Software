namespace GuardeSoftwareAPI.Entities
{
    public class MonthlyIncreaseSetting
    {
        public int Id { get; set; }
        public DateTime EffectiveDate { get; set; }
        public decimal Percentage { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}