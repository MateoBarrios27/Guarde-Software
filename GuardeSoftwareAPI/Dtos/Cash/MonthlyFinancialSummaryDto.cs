public class MonthlyFinancialSummaryDto {
    public decimal TotalSystemIncome { get; set; } // Desde tabla payments
    public decimal PendingCollection { get; set; } // Deuda total clientes
    public decimal TotalManualExpenses { get; set; } // Suma de items manuales
    public decimal TotalAdvancePayments { get; set; } // Pagos adelantados  
    public decimal NetBalance { get; set; } // Income - Expenses
}