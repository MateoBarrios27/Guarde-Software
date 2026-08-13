namespace GuardeSoftwareAPI.Dtos.AccountMovement
{
    public class PlannedPaymentIncreaseDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Percentage { get; set; }
        public decimal NewRentAmount { get; set; }
    }

    public class PlanClientPaymentDto
    {
        public int ClientId { get; set; }
        public int Months { get; set; }
        public bool ChargeHalfSixthMonth { get; set; } = true;
        public List<PlannedPaymentIncreaseDto> AppliedIncreases { get; set; } = [];
    }

    public class PaymentPlanningMonthDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsHalfPromotion { get; set; }
    }

    public class PaymentPlanningIncreaseDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class PaymentPlanningContextDto
    {
        public int ClientId { get; set; }
        public int Months { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal BaseRent { get; set; }
        public int IncreaseFrequencyMonths { get; set; }
        public DateTime? IncreaseAnchorDate { get; set; }
        public bool HasSixMonthPromotion { get; set; }
        public bool IsPriceLocked { get; set; }
        public List<PaymentPlanningIncreaseDto> Increases { get; set; } = [];
    }

    public class PlannedPaymentResultDto
    {
        public int CreatedDebits { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsPriceLocked { get; set; }
        public List<PaymentPlanningMonthDto> Months { get; set; } = [];
    }
}
