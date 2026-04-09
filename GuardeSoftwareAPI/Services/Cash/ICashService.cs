using GuardeSoftwareAPI.Dtos.Cash;
namespace GuardeSoftwareAPI.Services.cash
{
    public interface ICashService
    {
        Task<List<CashFlowItemDto>> GetItemsAsync(int month, int year);
        Task<int> UpsertItemAsync(CashFlowItemDto item, int month, int year);
        Task DeleteItemAsync(int id);
        Task<List<FinancialAccountDto>> GetAccountsAsync();
        Task UpdateAccountBalanceAsync(int id, decimal balance);
        Task<MonthlyFinancialSummaryDto> GetMonthlySummaryAsync(int month, int year);
        Task<int> CreateAccountAsync(FinancialAccountDto account);
        Task DeleteAccountAsync(int id);
        Task UpdateItemsOrderAsync(List<CashItemOrderDto> itemsOrder);
        Task UpdateAccountsOrderAsync(List<AccountOrderDto> accountsOrder);
    }
}