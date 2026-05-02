public class MonthlyFinancialSummaryDto {
    public decimal TotalSystemIncome { get; set; }
    public decimal PendingCollection { get; set; }
    public decimal TotalManualExpenses { get; set; } 
    public decimal TotalAdvancePayments { get; set; }
    public decimal NetBalance { get; set; } 
    public decimal Abono { get; set; }
    public decimal IvaFacturaA { get; set; }
    public decimal IvaFacturaB { get; set; }
}