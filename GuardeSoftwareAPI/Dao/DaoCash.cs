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
            // Agregamos display_order al SELECT y al ORDER BY
            string query = @"
                SELECT item_id, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order
                FROM cash_flow_items
                WHERE month = @Month AND year = @Year
                ORDER BY display_order ASC, movement_date ASC";

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
                    Date = row["movement_date"] != DBNull.Value ? Convert.ToDateTime(row["movement_date"]) : null,
                    Description = row["description"].ToString(),
                    Comment = row["comment"] != DBNull.Value ? row["comment"].ToString() : null,
                    Depo = Convert.ToDecimal(row["amount_depo"]),
                    Casa = Convert.ToDecimal(row["amount_casa"]),
                    IsPaid = Convert.ToBoolean(row["is_paid"]),
                    Retiros = Convert.ToDecimal(row["amount_retiros"]),
                    Extras = Convert.ToDecimal(row["amount_extras"]),
                    // Mapeamos el orden:
                    DisplayOrder = row["display_order"] != DBNull.Value ? Convert.ToInt32(row["display_order"]) : 0
                });
            }
            return list;
        }

        public async Task<int> UpsertItemAsync(CashFlowItemDto item, int month, int year)
        {
            string query = "";
            
            if (item.Id == null || item.Id == 0)
            {
                query = @"
                    DECLARE @InsertedId TABLE (Id INT);
                    DECLARE @NewDisplayOrder INT;

                    SELECT @NewDisplayOrder = ISNULL(MAX(display_order), 0) + 1 FROM cash_flow_items;

                    INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order)
                    OUTPUT INSERTED.item_id INTO @InsertedId
                    VALUES (@Month, @Year, @Date, @Desc, @Comment, @Depo, @Casa, @IsPaid, @Retiros, @Extras, @NewDisplayOrder);

                    UPDATE cash_flow_items
                    SET comment = @Comment,
                        movement_date = CASE WHEN @Date IS NULL THEN NULL ELSE DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END) END
                    WHERE description = @Desc
                    AND (year > @Year OR (year = @Year AND month > @Month));

                    INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order)
                    SELECT DISTINCT 
                        month, 
                        year, 
                        CASE WHEN @Date IS NULL THEN NULL ELSE DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END) END,
                        @Desc, @Comment, 0, 0, 0, 0, 0, @NewDisplayOrder
                    FROM cash_flow_items
                    WHERE (year > @Year OR (year = @Year AND month > @Month))
                    AND NOT EXISTS (SELECT 1 FROM cash_flow_items c2 WHERE c2.description = @Desc AND c2.month = cash_flow_items.month AND c2.year = cash_flow_items.year);

                    SELECT Id FROM @InsertedId;";
            }
            else
            {
                query = @"
                    DECLARE @CurrentDisplayOrder INT;
                    DECLARE @OldDesc NVARCHAR(255);
                    
                    SELECT @CurrentDisplayOrder = display_order, @OldDesc = description 
                    FROM cash_flow_items 
                    WHERE item_id = @Id;

                    UPDATE cash_flow_items 
                    SET movement_date = @Date, description = @Desc, comment = @Comment, amount_depo = @Depo, amount_casa = @Casa, is_paid = @IsPaid, amount_retiros = @Retiros, amount_extras = @Extras
                    WHERE item_id = @Id;

                    UPDATE cash_flow_items
                    SET description = @Desc,
                        comment = @Comment,
                        movement_date = CASE WHEN @Date IS NULL THEN NULL ELSE DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END) END
                    WHERE description = @OldDesc
                    AND (year > @Year OR (year = @Year AND month > @Month));

                    INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order)
                    SELECT DISTINCT 
                        month, 
                        year, 
                        CASE WHEN @Date IS NULL THEN NULL ELSE DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END) END,
                        @Desc, @Comment, 0, 0, 0, 0, 0, @CurrentDisplayOrder
                    FROM cash_flow_items
                    WHERE (year > @Year OR (year = @Year AND month > @Month))
                    AND NOT EXISTS (SELECT 1 FROM cash_flow_items c2 WHERE c2.description = @Desc AND c2.month = cash_flow_items.month AND c2.year = cash_flow_items.year);

                    SELECT @Id;";
            }

            var parameters = new[] {
                new SqlParameter("@Id", item.Id),
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year),
                new SqlParameter("@Date", (object?)item.Date ?? DBNull.Value),
                new SqlParameter("@Desc", item.Description ?? ""),
                new SqlParameter("@Comment", (object?)item.Comment ?? DBNull.Value),
                new SqlParameter("@Depo", item.Depo),
                new SqlParameter("@Casa", item.Casa),
                new SqlParameter("@IsPaid", item.IsPaid),
                new SqlParameter("@Retiros", item.Retiros),
                new SqlParameter("@Extras", item.Extras)
            };

            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToInt32(result);
        }

        public async Task DeleteItemAsync(int id)
        {
            string query = @"
                DECLARE @Desc NVARCHAR(255);
                DECLARE @ItemMonth INT;
                DECLARE @ItemYear INT;

                SELECT @Desc = description, @ItemMonth = month, @ItemYear = year 
                FROM cash_flow_items 
                WHERE item_id = @Id;

                DELETE FROM cash_flow_items WHERE item_id = @Id;

                -- ELIMINACIÓN EN CASCADA (Si el borrado es en el mes actual O un mes futuro)
                IF (@ItemYear > YEAR(GETDATE()) OR (@ItemYear = YEAR(GETDATE()) AND @ItemMonth >= MONTH(GETDATE())))
                BEGIN
                    DELETE FROM cash_flow_items 
                    WHERE description = @Desc 
                    -- Borramos de los meses posteriores al mes donde se ejecutó la acción
                    AND (year > @ItemYear OR (year = @ItemYear AND month > @ItemMonth))
                END";

            await _accessDB.ExecuteCommandAsync(query, new[] { new SqlParameter("@Id", id) });
        }

        #endregion

        #region 2. Cuentas Financieras

        public async Task<List<FinancialAccountDto>> GetAccountsAsync()
        {
            var list = new List<FinancialAccountDto>();
            string query = "SELECT account_id, name, type, current_balance, currency, display_order FROM financial_accounts ORDER BY display_order ASC, name ASC";
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

            // Le agregamos 'display_order' al INSERT y al SELECT
            string query = @"
                INSERT INTO cash_flow_items (
                    month, year, movement_date, description, comment, 
                    amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order
                )
                SELECT 
                    @CurrentMonth, @CurrentYear, DATEFROMPARTS(@CurrentYear, @CurrentMonth, 1), 
                    description, comment, 0, 0, 0, 0, 0, display_order
                FROM cash_flow_items
                WHERE month = @PrevMonth AND year = @PrevYear
                AND is_confirmed = 1";

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
                DECLARE @NewOrder INT;
                SELECT @NewOrder = ISNULL(MAX(display_order), 0) + 1 FROM financial_accounts;

                INSERT INTO financial_accounts (name, type, current_balance, currency, display_order) 
                VALUES (@Name, @Type, @Balance, @Currency, @NewOrder);

                SELECT SCOPE_IDENTITY();";

            var parameters = new[] {
                new SqlParameter("@Name", SqlDbType.NVarChar) { Value = account.Name },
                new SqlParameter("@Type", SqlDbType.NVarChar) { Value = account.Type },
                new SqlParameter("@Balance", SqlDbType.Decimal) { Value = account.Balance != null ? account.Balance : 0 },
                new SqlParameter("@Currency", SqlDbType.NVarChar) { Value = account.Currency != null ? account.Currency : "" }
            };

            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            
            if (result == DBNull.Value || result == null)
            {
                return 0;
            }

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
            string query = @"
                -- 1. BALANCE GLOBAL POSITIVO (Filtro de seguridad)
                -- Solo consideramos clientes que 'hoy' tienen plata a favor.
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

                -- 2. CRÉDITOS REALES DEL MES (Dinero a favor generado este mes)
                -- Aquí sumamos Pagos, Notas de Crédito, Ajustes, etc.
                -- Todo lo que NO sea 'DEBITO'.
                MonthlyCredits AS (
                    SELECT 
                        r.client_id,
                        ISNULL(SUM(am.amount), 0) as TotalCreditedInMonth
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE am.movement_type <> 'DEBITO' -- <--- EL CAMBIO CLAVE
                    AND MONTH(am.movement_date) = @Month
                    AND YEAR(am.movement_date) = @Year
                    GROUP BY r.client_id
                ),

                -- 3. DÉBITOS REALES DEL MES (Lo que se cobró: Alquiler, Proporcionales, etc)
                MonthlyDebits AS (
                    SELECT 
                        r.client_id,
                        ISNULL(SUM(am.amount), 0) as TotalDebitedInMonth
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE am.movement_type = 'DEBITO' 
                    AND MONTH(am.movement_date) = @Month
                    AND YEAR(am.movement_date) = @Year
                    GROUP BY r.client_id
                )

                -- CÁLCULO FINAL:
                -- (Créditos del Mes - Débitos del Mes) = Excedente generado este mes.
                -- Ajustado por el tope de su saldo global real actual.
                SELECT ISNULL(SUM(
                    CASE 
                        -- Si el excedente del mes es mayor que su saldo global actual, 
                        -- significa que parte de ese crédito se usó para cubrir deuda vieja que ya no existe,
                        -- o que el saldo se consumió después. Solo tomamos el 'saldo vivo'.
                        WHEN (mc.TotalCreditedInMonth - ISNULL(md.TotalDebitedInMonth, 0)) > pb.CurrentGlobalBalance 
                        THEN pb.CurrentGlobalBalance
                        
                        -- Caso normal: El adelanto es la diferencia pura generada en este mes
                        ELSE (mc.TotalCreditedInMonth - ISNULL(md.TotalDebitedInMonth, 0))
                    END
                ), 0)
                FROM MonthlyCredits mc
                INNER JOIN PositiveBalances pb ON mc.client_id = pb.client_id
                LEFT JOIN MonthlyDebits md ON mc.client_id = md.client_id
                WHERE 
                    -- Solo sumamos si los Créditos superaron a los Débitos este mes
                    mc.TotalCreditedInMonth > ISNULL(md.TotalDebitedInMonth, 0);
            ";

            var parameters = new[] {
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            };

            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToDecimal(result);
        }

        public async Task UpdateItemsOrderAsync(List<CashItemOrderDto> itemsOrder)
        {
            using (var connection = _accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    string query = @"
                        DECLARE @Desc NVARCHAR(255);
                        DECLARE @ItemMonth INT;
                        DECLARE @ItemYear INT;
                        
                        -- 1. Buscamos el nombre, mes y año exacto del ítem que movió el usuario
                        SELECT @Desc = description, @ItemMonth = month, @ItemYear = year
                        FROM cash_flow_items 
                        WHERE item_id = @Id;
                        
                        -- 2. Si encontró el concepto, actualizamos su posición en este mes y hacia el futuro
                        IF @Desc IS NOT NULL AND @Desc <> ''
                        BEGIN
                            UPDATE cash_flow_items 
                            SET display_order = @DisplayOrder 
                            WHERE description = @Desc
                            -- ACÁ ESTÁ EL FILTRO MÁGICO: Solo afecta al mes de origen y los siguientes
                            AND (year > @ItemYear OR (year = @ItemYear AND month >= @ItemMonth));
                        END";
                    
                    foreach (var item in itemsOrder)
                    {
                        await _accessDB.ExecuteCommandTransactionAsync(query, new[] {
                            new SqlParameter("@DisplayOrder", item.DisplayOrder),
                            new SqlParameter("@Id", item.Id)
                        }, connection, transaction);
                    }
                    
                    await transaction.CommitAsync();
                }
            }
        }
        public async Task UpdateAccountsOrderAsync(List<AccountOrderDto> accountsOrder)
        {
            using (var connection = _accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    string query = "UPDATE financial_accounts SET display_order = @DisplayOrder WHERE account_id = @Id";
                    
                    foreach (var item in accountsOrder)
                    {
                        await _accessDB.ExecuteCommandTransactionAsync(query, new[] {
                            new SqlParameter("@DisplayOrder", item.DisplayOrder),
                            new SqlParameter("@Id", item.Id)
                        }, connection, transaction);
                    }
                    await transaction.CommitAsync();
                }
            }
        }
    }
}