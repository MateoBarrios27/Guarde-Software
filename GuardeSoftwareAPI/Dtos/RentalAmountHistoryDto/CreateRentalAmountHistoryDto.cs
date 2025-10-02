namespace GuardeSoftwareAPI.Dtos.RentalAmountHistoryDto
{
    public class CreateRentalAmountHistoryDto
    {
        public int RentalId { get; set; }
        public decimal Amount { get; set; }
        public DateTime StartDate { get; set; }
    }
}