namespace GuardeSoftwareAPI.Dtos.AccountMovement;

public sealed class MovementStatisticsDto
{
    public int MovementId { get; init; }
    public int RentalId { get; init; }
    public DateTime MovementDate { get; init; }
    public decimal PreviousBalance { get; init; }
    public decimal InterestAmount { get; init; }
    public decimal? CurrentRent { get; init; }
    public decimal Balance { get; init; }
}
