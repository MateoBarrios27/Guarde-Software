using GuardeSoftwareAPI.Dtos.Cash;
using Microsoft.Data.SqlClient;
using System.Data;

namespace GuardeSoftwareAPI.Dao
{
    public class CashDao
    {
        private readonly AccessDB _accessDB;

        public CashDao(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        #region 1. Items de Caja (Tabla Manual)

        public async Task<List<CashFlowItemDto>> GetItemsAsync(int month, int year)
        {
            var list = new List<CashFlowItemDto>();
            string query = @"
                SELECT item_id, movement_date, description, amount_depo, amount_casa, amount_pagado, amount_retiros, amount_extras
                FROM cash_flow_items
                WHERE month = @Month AND year = @Year
                ORDER BY movement_date ASC";

            var parameters = new[] {
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            };

            var dt = await _accessDB.GetTableAsync("CashFlowItems", query, parameters);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new CashFlowItemDto
                {
                    Id = Convert.ToInt32(row["item_id"]),
                    Date = Convert.ToDateTime(row["movement_date"]),
                    Description = row["description"].ToString(),
                    Depo = Convert.ToDecimal(row["amount_depo"]),
                    Casa = Convert.ToDecimal(row["amount_casa"]),
                    Pagado = Convert.ToDecimal(row["amount_pagado"]),
                    Retiros = Convert.ToDecimal(row["amount_retiros"]),
                    Extras = Convert.ToDecimal(row["amount_extras"])
                });
            }
            return list;
        }

        public async Task<int> UpsertItemAsync(CashFlowItemDto item, int month, int year)
        {
            // Lógica: Si ID es nulo o 0, INSERT. Si no, UPDATE.
            string query = "";
            
            if (item.Id == null || item.Id == 0)
            {
                query = @"
                    INSERT INTO cash_flow_items (month, year, movement_date, description, amount_depo, amount_casa, amount_pagado, amount_retiros, amount_extras)
                    OUTPUT INSERTED.item_id
                    VALUES (@Month, @Year, @Date, @Desc, @Depo, @Casa, @Pagado, @Retiros, @Extras)";
            }
            else
            {
                query = @"
                    UPDATE cash_flow_items 
                    SET movement_date = @Date, 
                        description = @Desc, 
                        amount_depo = @Depo, 
                        amount_casa = @Casa, 
                        amount_pagado = @Pagado, 
                        amount_retiros = @Retiros, 
                        amount_extras = @Extras
                    WHERE item_id = @Id";
            }

            var parameters = new[] {
                new SqlParameter("@Id", item.Id),
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year),
                new SqlParameter("@Date", item.Date),
                new SqlParameter("@Desc", item.Description ?? ""),
                new SqlParameter("@Depo", item.Depo),
                new SqlParameter("@Casa", item.Casa),
                new SqlParameter("@Pagado", item.Pagado),
                new SqlParameter("@Retiros", item.Retiros),
                new SqlParameter("@Extras", item.Extras)
            };

            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToInt32(result);
        }

        public async Task DeleteItemAsync(int id)
        {
            string query = "DELETE FROM cash_flow_items WHERE item_id = @Id";
            await _accessDB.ExecuteCommandAsync(query, new[] { new SqlParameter("@Id", id) });
        }

        #endregion

        #region 2. Cuentas Financieras

        public async Task<List<FinancialAccountDto>> GetAccountsAsync()
        {
            var list = new List<FinancialAccountDto>();
            string query = "SELECT account_id, name, type, currency, current_balance FROM financial_accounts";
            var dt = await _accessDB.GetTableAsync("Accounts", query);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new FinancialAccountDto
                {
                    Id = Convert.ToInt32(row["account_id"]),
                    Name = row["name"].ToString(),
                    Type = row["type"].ToString(),
                    Currency = row["currency"].ToString(),
                    Balance = Convert.ToDecimal(row["current_balance"])
                });
            }
            return list;
        }

        public async Task UpdateAccountBalanceAsync(int id, decimal balance)
        {
            string query = "UPDATE financial_accounts SET current_balance = @Balance, last_updated = GETDATE() WHERE account_id = @Id";
            var parameters = new[] {
                new SqlParameter("@Balance", balance),
                new SqlParameter("@Id", id)
            };
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        #endregion

        #region 3. Cálculos para Resumen (Automático)

        // Obtiene la suma total de pagos reales registrados en el sistema para ese mes
        public async Task<decimal> GetSystemIncomeAsync(int month, int year)
        {
            string query = @"
                SELECT ISNULL(SUM(amount), 0)
                FROM payments
                WHERE MONTH(payment_date) = @Month 
                  AND YEAR(payment_date) = @Year 
                  "; 

            var result = await _accessDB.ExecuteScalarAsync(query, new[] { 
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            });
            return Convert.ToDecimal(result);
        }

        // Obtiene la deuda total actual (sumatoria de alquileres impagos o saldos negativos)
        // Esta query puede variar según cómo calcules la deuda en tu sistema hoy
        public async Task<decimal> GetPendingCollectionAsync(int month, int year)
        {
            // Opción A: Sumar saldos de clientes con deuda (balance > 0 o < 0 según tu lógica)
            // Asumiré que balance positivo es deuda según tu contexto anterior ("Morosos")
            DateTime startDate = new(year, month, 1);
            SqlParameter[] parameters = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate }
            ];

            string query = "SELECT ISNULL(SUM( CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END ), 0) FROM account_movements WHERE movement_date < @StartDate";
            
            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToDecimal(result);
        }

        // Obtiene totales de la tabla manual para el resumen
        public async Task<decimal> GetManualExpensesTotalAsync(int month, int year)
        {
            // Sumamos Casa + Pagado + Retiros + Extras (Excluyendo Depo si es ingreso)
            string query = @"
                SELECT ISNULL(SUM(amount_casa + amount_pagado + amount_retiros + amount_extras), 0)
                FROM cash_flow_items
                WHERE month = @Month AND year = @Year";

            var result = await _accessDB.ExecuteScalarAsync(query, new[] {
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            });
            return Convert.ToDecimal(result);
        }

        #endregion


        public async Task CopyConceptsFromPreviousMonthAsync(int currentMonth, int currentYear)
        {
            var date = new DateTime(currentYear, currentMonth, 1).AddMonths(-1);
            int prevMonth = date.Month;
            int prevYear = date.Year;

            // CORRECCIÓN CLAVE:
            // 1. Insertamos @CurrentMonth y @CurrentYear en las columnas month/year
            // 2. Generamos la fecha movement_date forzada al día 1 del mes actual (DATEFROMPARTS)
            // 3. Copiamos los montos del mes anterior
            
            string query = @"
                INSERT INTO cash_flow_items (
                    month, year, movement_date, description, 
                    amount_depo, amount_casa, amount_pagado, amount_retiros, amount_extras
                )
                SELECT 
                    @CurrentMonth,  -- Forzamos el MES ACTUAL
                    @CurrentYear,   -- Forzamos el AÑO ACTUAL
                    DATEFROMPARTS(@CurrentYear, @CurrentMonth, 1), -- Ponemos fecha día 1 por defecto
                    description, 
                    amount_depo, amount_casa, amount_pagado, amount_retiros, amount_extras
                FROM cash_flow_items
                WHERE month = @PrevMonth AND year = @PrevYear
                AND is_confirmed = 1"; // Opcional: filtrar por confirmados

            var parameters = new[] {
                new SqlParameter("@CurrentMonth", currentMonth),
                new SqlParameter("@CurrentYear", currentYear),
                new SqlParameter("@PrevMonth", prevMonth),
                new SqlParameter("@PrevYear", prevYear)
            };

            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        // En la región #region 2. Cuentas Financieras

        public async Task<int> CreateAccountAsync(FinancialAccountDto account)
        {
            string query = @"
                INSERT INTO financial_accounts (name, type, currency, current_balance, last_updated)
                OUTPUT INSERTED.account_id
                VALUES (@Name, @Type, 'ARS', @Balance, GETDATE())"; // Default ARS por ahora

            var parameters = new[] {
                new SqlParameter("@Name", account.Name),
                new SqlParameter("@Type", account.Type),
                new SqlParameter("@Balance", account.Balance)
            };

            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToInt32(result);
        }

        public async Task DeleteAccountAsync(int id)
        {
            string query = "DELETE FROM financial_accounts WHERE account_id = @Id";
            await _accessDB.ExecuteCommandAsync(query, new[] { new SqlParameter("@Id", id) });
        }

        public async Task<decimal> GetCalculatedIvaAsync(int month, int year)
        {
            string query = @"
                SELECT ISNULL(SUM(amount), 0)
                FROM payments
                WHERE payment_method_id = 2
                AND MONTH(payment_date) = @Month 
                AND YEAR(payment_date) = @Year";

            var result = await _accessDB.ExecuteScalarAsync(query, new[] { 
                new SqlParameter("@Month", month), 
                new SqlParameter("@Year", year) 
            });
            
            decimal totalTransfers = Convert.ToDecimal(result);
            return totalTransfers * 0.21m;
        }

        public async Task<decimal> GetTotalAdvancePaymentsAsync(int month, int year)
        {
            // Lógica Corregida: "Intersección entre lo pagado este mes y el saldo a favor actual"
            string query = @"
                -- CTE 1: BALANCE GLOBAL POSITIVO
                -- Buscamos clientes que AL DÍA DE HOY tengan saldo a favor.
                WITH PositiveBalances AS (
                    SELECT
                        r.client_id,
                        SUM(
                            CASE 
                                WHEN am.movement_type = 'DEBITO' THEN -am.amount 
                                ELSE am.amount 
                            END
                        ) AS CurrentGlobalBalance
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE r.active = 1
                    GROUP BY r.client_id
                    HAVING SUM(CASE WHEN am.movement_type = 'DEBITO' THEN -am.amount ELSE am.amount END) > 0
                ),

                -- CTE 2: PAGOS DEL MES
                -- Buscamos cuánto pagaron efectivamente en el mes consultado.
                MonthlyPayments AS (
                    SELECT 
                        client_id, 
                        SUM(amount) as TotalPaidInMonth
                    FROM payments
                    WHERE MONTH(payment_date) = @Month
                    AND YEAR(payment_date) = @Year
                    GROUP BY client_id
                )

                -- CÁLCULO FINAL:
                -- Cruzamos los que pagaron este mes CON los que terminaron con saldo positivo.
                -- Si pagó 73.000 pero su saldo final es 9.700 -> El adelanto real es 9.700.
                -- Si pagó 10.000 y su saldo final es 50.000 (venía acumulando) -> El adelanto generado ESTE MES es 10.000.
                SELECT ISNULL(SUM(
                    CASE 
                        WHEN mp.TotalPaidInMonth < pb.CurrentGlobalBalance THEN mp.TotalPaidInMonth
                        ELSE pb.CurrentGlobalBalance
                    END
                ), 0)
                FROM MonthlyPayments mp
                INNER JOIN PositiveBalances pb ON mp.client_id = pb.client_id;
            ";

            var parameters = new[] {
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            };

            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToDecimal(result);
        }
    }
}