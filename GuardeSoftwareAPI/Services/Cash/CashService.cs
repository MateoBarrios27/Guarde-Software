using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Cash;

namespace GuardeSoftwareAPI.Services.cash
{
    public class CashService : ICashService
    {
        private readonly CashDao _dao;

        public CashService(AccessDB accessDB)
        {
            _dao = new CashDao(accessDB);
        }

        public async Task<List<CashFlowItemDto>> GetItemsAsync(int month, int year)
        {
            var items = await _dao.GetItemsAsync(month, year);

            if (items.Count == 0) if (items.Count == 0)
            {
                await _dao.CopyConceptsFromPreviousMonthAsync(month, year);
                items = await _dao.GetItemsAsync(month, year);
            }
            
            decimal ivaValue = await _dao.GetCalculatedIvaAsync(month, year);

            var ivaItem = items.FirstOrDefault(x => x.Description == "IVA (21% Transferencias)");

            if (ivaItem != null)
            {
                ivaItem.Depo = ivaValue; 
                
            }

            return items;
        }

        public async Task<int> UpsertItemAsync(CashFlowItemDto item, int month, int year)
        {
            return await _dao.UpsertItemAsync(item, month, year);
        }

        public async Task DeleteItemAsync(int id)
        {
            await _dao.DeleteItemAsync(id);
        }

        public async Task<List<FinancialAccountDto>> GetAccountsAsync(int month, int year)
        {
            return await _dao.GetAccountsAsync(month, year);
        }

        public async Task UpdateAccountBalanceAsync(int id, decimal balance)
        {
            await _dao.UpdateAccountBalanceAsync(id, balance);
        }

        public async Task<MonthlyFinancialSummaryDto> GetMonthlySummaryAsync(int month, int year)
        {
            decimal systemIncome = await _dao.GetSystemIncomeAsync(month, year);
            
            decimal manualExpenses = await _dao.GetManualExpensesTotalAsync(month, year);
            
            decimal pending = await _dao.GetPendingCollectionAsync(month, year);

            decimal advancePayments = await _dao.GetTotalAdvancePaymentsAsync(month, year);

            decimal netBalance = systemIncome - manualExpenses;

            return new MonthlyFinancialSummaryDto
            {
                TotalSystemIncome = systemIncome,
                TotalManualExpenses = manualExpenses,
                TotalAdvancePayments = advancePayments,
                NetBalance = netBalance,
                PendingCollection = pending
            };
        }

        public async Task<int> CreateAccountAsync(FinancialAccountDto account, int month, int year)
        {
            return await _dao.CreateAccountAsync(account, month, year);
        }

        public async Task DeleteAccountAsync(int id)
        {
            await _dao.DeleteAccountAsync(id);
        }
        
        public async Task UpdateItemsOrderAsync(List<CashItemOrderDto> itemsOrder)
        {
            await _dao.UpdateItemsOrderAsync(itemsOrder);
        }

        public async Task UpdateAccountsOrderAsync(List<AccountOrderDto> accountsOrder)
        {
            await _dao.UpdateAccountsOrderAsync(accountsOrder);
        }

        public async Task<decimal> GetUsdRateAsync(int month, int year)
        {
            return await _dao.GetUsdRateAsync(month, year);
        }

        public async Task UpdateUsdRateAsync(decimal rate, int month, int year)
        {
            await _dao.UpdateUsdRateAsync(rate, month, year);
        }
    }
}