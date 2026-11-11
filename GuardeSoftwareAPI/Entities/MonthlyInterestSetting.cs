namespace GuardeSoftwareAPI.Entities
{
    public class MonthlyInterestSetting
    {
        public int Id { get; set; }
        /// <summary>First day of the configured month.</summary>
        public DateTime EffectiveDate { get; set; }
        public decimal Percentage { get; set; }
        /// <summary>Day of the month the interests job runs.</summary>
        public int ApplyDay { get; set; }
        /// <summary>Last day to pay without surcharge (ApplyDay - 1).</summary>
        public int CutoffDay => ApplyDay - 1;
        /// <summary>True when the month can no longer be edited (already applied or past).</summary>
        public bool IsLocked { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class MonthlyInterestRun
    {
        public DateTime Period { get; set; }
        public DateTime AppliedAt { get; set; }
        public decimal Percentage { get; set; }
        public int ApplyDay { get; set; }
        public bool UsedDefault { get; set; }
        public int AffectedRentals { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
