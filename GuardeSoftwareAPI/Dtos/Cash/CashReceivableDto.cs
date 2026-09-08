using System.ComponentModel.DataAnnotations;

namespace GuardeSoftwareAPI.Dtos.Cash;

public class CashReceivableInput : IValidatableObject
{
    [Required, StringLength(500)] public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999.99", ParseLimitsInInvariantCulture = true)] public decimal TotalAmount { get; set; }
    [StringLength(1000)] public string Notes { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Description)) yield return new ValidationResult("Ingresá la persona y el concepto de la cuenta.", new[] { nameof(Description) });
        if (Date == default) yield return new ValidationResult("Ingresá una fecha válida.", new[] { nameof(Date) });
        if (decimal.Round(TotalAmount, 2) != TotalAmount) yield return new ValidationResult("El total admite hasta dos decimales.", new[] { nameof(TotalAmount) });
    }
}

public class CashReceivableDto : CashReceivableInput
{
    public int Id { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public List<CashReceivablePaymentDto> Payments { get; set; } = new();
}

public class CashReceivablePaymentInput : IValidatableObject
{
    public DateTime Date { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999.99", ParseLimitsInInvariantCulture = true)] public decimal Amount { get; set; }
    [StringLength(500)] public string Comment { get; set; } = "";
    public Guid RequestId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Date == default) yield return new ValidationResult("Ingresá una fecha válida.", new[] { nameof(Date) });
        if (RequestId == Guid.Empty) yield return new ValidationResult("Falta el identificador del pago.", new[] { nameof(RequestId) });
        if (decimal.Round(Amount, 2) != Amount) yield return new ValidationResult("El importe admite hasta dos decimales.", new[] { nameof(Amount) });
    }
}

public class CashReceivablePaymentDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Comment { get; set; } = "";
}
