namespace GuardeSoftwareAPI.Dtos.Locker;

public sealed class LockerClientHistoryDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal? PaymentIdentifier { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
}
