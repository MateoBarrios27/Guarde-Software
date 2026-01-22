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

        // --- Items Manuales ---
        public async Task<List<CashFlowItemDto>> GetItemsAsync(int month, int year)
        {
            return await _dao.GetItemsAsync(month, year);
        }

        public async Task<int> UpsertItemAsync(CashFlowItemDto item)
        {
            // Extraer mes y año de la fecha del movimiento para mantener consistencia
            int month = item.Date.Month;
            int year = item.Date.Year;
            return await _dao.UpsertItemAsync(item, month, year);
        }

        public async Task DeleteItemAsync(int id)
        {
            await _dao.DeleteItemAsync(id);
        }

        // --- Cuentas ---
        public async Task<List<FinancialAccountDto>> GetAccountsAsync()
        {
            return await _dao.GetAccountsAsync();
        }

        public async Task UpdateAccountBalanceAsync(int id, decimal balance)
        {
            await _dao.UpdateAccountBalanceAsync(id, balance);
        }

        // --- Resumen Mensual (El núcleo de la lógica financiera) ---
        public async Task<MonthlyFinancialSummaryDto> GetMonthlySummaryAsync(int month, int year)
        {
            // 1. Ingresos del Sistema (Automático desde Pagos)
            decimal systemIncome = await _dao.GetSystemIncomeAsync(month, year);
            
            // 2. Gastos Manuales (Suma de la grilla de caja)
            decimal manualExpenses = await _dao.GetManualExpensesTotalAsync(month, year);
            
            // 3. Deuda pendiente (Estado actual general)
            decimal pending = await _dao.GetPendingCollectionAsync(month, year);

            // 4. Calcular Neto
            // Fórmula: Ingresos - Gastos. 
            // Nota: Aquí podrías sumar 'Total Advance Payments' si tuvieras lógica para detectarlos separadamente.
            decimal netBalance = systemIncome - manualExpenses;

            return new MonthlyFinancialSummaryDto
            {
                TotalSystemIncome = systemIncome,
                TotalManualExpenses = manualExpenses,
                // TotalAdvancePayments = 0, // Placeholder si quieres implementarlo a futuro
                NetBalance = netBalance,
                PendingCollection = pending
            };
        }
    }
}