namespace GuardeSoftwareAPI.Dtos.Client;

// Next-month projection for the Clients table. Amounts use the table convention
// (negative = debt, positive = credit) and come from the same calculation as the
// projected account statement.
public class ClientProjectedNextMonthDto
{
    public int ClientId { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal CurrentRent { get; set; }
    public decimal Balance { get; set; }
    // Total the projected statement asks the client to pay (includes the payment
    // identifier cents; zero when there is nothing to pay).
    public decimal StatementTotal { get; set; }
    // Increase included in CurrentRent (null when none) and whether an increase is due
    // but the month has no configured percentage (the current rent is shown instead).
    public decimal? IncreasePercentage { get; set; }
    public bool IncreasePercentageMissing { get; set; }
}

public class ClientProjectedNextMonthResultDto
{
    public DateTime TargetMonth { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<ClientProjectedNextMonthDto> Items { get; set; } = [];
    // Clients whose projection could not be calculated; the table shows them as unknown.
    public List<int> FailedClientIds { get; set; } = [];
}
