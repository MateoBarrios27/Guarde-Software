namespace GuardeSoftwareAPI.Dtos.Client;

public class ClientFinancialDto {
    public decimal PreviousBalance { get; set; }
    public decimal Surcharge { get; set; }
    public decimal CurrentBalance { get; set; }

    // Not part of the statement body. They let Clients show the same projection
    // without a second calculation: identifier cents, the rent used by the
    // statement and the signed debt (positive = debt, negative = credit) that
    // CurrentBalance clamps to zero.
    public decimal PaymentIdentifier { get; set; }
    public decimal Rent { get; set; }
    public decimal NetDebt { get; set; }

    // Projected statement only. Percentage applied to the projected rent (null when no
    // increase falls in the month) and whether an increase is due but the month has no
    // percentage in Configuración, so the current rent is used and flagged, not guessed.
    public decimal? RentIncreasePercentage { get; set; }
    public bool RentIncreasePercentageMissing { get; set; }
}
