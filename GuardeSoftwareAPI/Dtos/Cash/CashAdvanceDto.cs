namespace GuardeSoftwareAPI.Dtos.Cash
{
    public class CashAdvanceDto
    {
        public int? Id { get; set; }
        public int ItemId { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
    }
}
