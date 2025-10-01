public class PendingRentalDTO
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int MonthsUnpaid { get; set; }
    public decimal Balance { get; set; }
    public decimal CurrentRent { get; set; }
    public decimal PendingAmount => CurrentRent - Balance;
    public bool IsPending => PendingAmount > 0;
}