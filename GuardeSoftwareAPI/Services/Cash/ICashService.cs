
namespace GuardeSoftwareAPI.Services.cash
{
    public interface ICashService
    {
        Task<List<CashFlowItemDto>> GetItemsAsync(int month, int year);
        Task<int> UpsertItemAsync(CashFlowItemDto item);
        Task DeleteItemAsync(int id);
        Task<List<FinancialAccountDto>> GetAccountsAsync();
        Task UpdateAccountBalanceAsync(int id, decimal balance);
        Task<MonthlyFinancialSummaryDto> GetMonthlySummaryAsync(int month, int year);
    }
}