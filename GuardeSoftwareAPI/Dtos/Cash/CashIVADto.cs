namespace GuardeSoftwareAPI.Dtos.Cash
{
    public class CashIVADto
    {
        public int? Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string? Comment { get; set; }
    }
}