namespace GuardeSoftwareAPI.Dtos.Client;
public class ClientReactivationContextDto
{
    public int? RentalId { get; set; }
    // Same sign as the UI: negative is debt, positive is a credit balance.
    public decimal Balance { get; set; }
}
