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
            else
            {   // IVA as a separate item if it doesn't exist yet
                // var newIva = new CashFlowItemDto
                // {
                //     Date = new DateTime(year, month, 1),
                //     Description = "IVA (21% Transferencias)",
                //     Depo = ivaValue, // Valor calculado
                //     Casa = 0, IsPaid = false, Retiros = 0, Extras = 0
                // };
                
                // int newId = await _dao.UpsertItemAsync(newIva, month, year);
                // newIva.Id = newId;
                
                // items.Insert(0, newIva);
            }

            return items;
        }

        public async Task<int> UpsertItemAsync(CashFlowItemDto item)
        {
            int month = item.Date.Month;
            int year = item.Date.Year;
            return await _dao.UpsertItemAsync(item, month, year);
        }

        public async Task DeleteItemAsync(int id)
        {
            await _dao.DeleteItemAsync(id);
        }

        public async Task<List<FinancialAccountDto>> GetAccountsAsync()
        {
            return await _dao.GetAccountsAsync();
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

        public async Task<int> CreateAccountAsync(FinancialAccountDto account)
        {
            return await _dao.CreateAccountAsync(account);
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
    }
}