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
            // 1. Obtener items manuales
            var items = await _dao.GetItemsAsync(month, year);

            // 2. Copia automática (si aplica)
            if (items.Count == 0) if (items.Count == 0)
            {
                await _dao.CopyConceptsFromPreviousMonthAsync(month, year);
                items = await _dao.GetItemsAsync(month, year);
            }
            
            // 3. Obtener Valor Calculado
            decimal ivaValue = await _dao.GetCalculatedIvaAsync(month, year);

            // 4. Buscar si ya existe la fila de IVA en la lista recuperada
            var ivaItem = items.FirstOrDefault(x => x.Description == "IVA (21% Transferencias)");

            if (ivaItem != null)
            {
                // Si existe, actualizamos SOLO el valor calculado (Depo) para que esté al día
                // Mantenemos Pagado, Retiros, etc. que vienen de la BD
                ivaItem.Depo = ivaValue; 
                
                // Opcional: Si quieres persistir este nuevo valor calculado en la BD ahora mismo:
                // await _dao.UpsertItemAsync(ivaItem, month, year);
            }
            else
            {
                // Si no existe, lo creamos y lo agregamos a la lista
                var newIva = new CashFlowItemDto
                {
                    Date = new DateTime(year, month, 1),
                    Description = "IVA (21% Transferencias)",
                    Depo = ivaValue, // Valor calculado
                    Casa = 0, Pagado = 0, Retiros = 0, Extras = 0
                };
                
                // Lo guardamos en BD para tener ID real y permitir editar Pagado después
                int newId = await _dao.UpsertItemAsync(newIva, month, year);
                newIva.Id = newId;
                
                items.Insert(0, newIva);
            }

            return items;
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

        public async Task<int> CreateAccountAsync(FinancialAccountDto account)
        {
            return await _dao.CreateAccountAsync(account);
        }

        public async Task DeleteAccountAsync(int id)
        {
            await _dao.DeleteAccountAsync(id);
        }
    }
}