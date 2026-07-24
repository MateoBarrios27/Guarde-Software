namespace GuardeSoftwareAPI.Dtos.Client
{
    public class RentalAmountHistoryItemDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        /// <summary>"active", "planned", or "past"</summary>
        public string Status { get; set; } = string.Empty;
    }

    public class CreateClientRentalAmountDto
    {
        public decimal Amount { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class UpdateClientRentalAmountDto
    {
        public decimal Amount { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
    }
}
