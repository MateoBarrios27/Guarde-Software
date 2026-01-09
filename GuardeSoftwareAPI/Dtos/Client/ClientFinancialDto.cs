namespace GuardeSoftwareAPI.Dtos.Client;

public class ClientFinancialDto {
    public decimal PreviousBalance { get; set; }
    public decimal Surcharge { get; set; }
    public decimal CurrentBalance { get; set; }
}

