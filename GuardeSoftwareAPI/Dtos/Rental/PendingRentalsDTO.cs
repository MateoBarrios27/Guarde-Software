public class PendingRentalDTO
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal PaymentIdentifier {get; set;}
    public int MonthsUnpaid { get; set; }
    public decimal Balance { get; set; }
    public decimal CurrentRent { get; set; }
    public string LockerIdentifiers { get; set; } = string.Empty;
    
    public int? PreferredPayment{ get; set; }
}
