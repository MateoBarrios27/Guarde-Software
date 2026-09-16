namespace GuardeSoftwareAPI.Dtos.Client;

public class ChangePaymentMethodDto
{
    public int PaymentMethodId { get; set; }
    public int ExpectedPaymentMethodId { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal Amount { get; set; }
}

public class PaymentMethodChangeContextDto
{
    public int PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = "";
    public decimal Commission { get; set; }
    public decimal Amount { get; set; }
    public DateTime? NextIncreaseDate { get; set; }
}
