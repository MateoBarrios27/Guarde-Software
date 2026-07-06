using GuardeSoftwareAPI.Dtos.Cash;
namespace GuardeSoftwareAPI.Services.cash
{
    public interface ICashService
    {
        Task<List<CashFlowItemDto>> GetItemsAsync(int month, int year);
        Task<int> UpsertItemAsync(CashFlowItemDto item, int month, int year);
        Task DeleteItemAsync(int id);
        Task<List<FinancialAccountDto>> GetAccountsAsync(int month, int year);
        Task UpdateAccountBalanceAsync(int id, decimal balance);
        Task<MonthlyFinancialSummaryDto> GetMonthlySummaryAsync(int month, int year);
        Task<int> CreateAccountAsync(FinancialAccountDto account, int month, int year);
        Task DeleteAccountAsync(int id);
        Task UpdateItemsOrderAsync(List<CashItemOrderDto> itemsOrder);
        Task UpdateAccountsOrderAsync(List<AccountOrderDto> accountsOrder);
        Task<decimal> GetUsdRateAsync(int month, int year);
        Task UpdateUsdRateAsync(decimal rate, int month, int year);
        Task UpdateAccountColorAsync(int id, string color);
        Task<bool> UpdateAccountNameAsync(int id, string name);
        Task<List<CashIVADto>> GetIvaComprasAsync(int month, int year);
        Task<int> AddIvaCompraAsync(CashIVADto dto);
        Task DeleteIvaCompraAsync(int id);
        Task<List<CashFlowItemDto>> GetHistoricalGroupedItemsAsync(DateTime fromDate, DateTime toDate);
        Task<List<CashAdvanceDto>> GetAdvancesAsync(int itemId);
        Task<int> AddAdvanceAsync(CashAdvanceDto dto);
        Task DeleteAdvanceAsync(int id);
    }
}