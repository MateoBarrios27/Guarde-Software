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
                    Date = Convert.ToDateTime(row["movement_date"]),
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

                    -- 1. Calculamos la última posición disponible en la tabla
                    SELECT @NewDisplayOrder = ISNULL(MAX(display_order), 0) + 1 FROM cash_flow_items;

                    -- 2. Insertamos el concepto en el mes actual asignándole esa última posición
                    INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order)
                    OUTPUT INSERTED.item_id INTO @InsertedId
                    VALUES (@Month, @Year, @Date, @Desc, @Comment, @Depo, @Casa, @IsPaid, @Retiros, @Extras, @NewDisplayOrder);

                    -- REPLICACIÓN AL FUTURO
                    IF (@Year > YEAR(GETDATE()) OR (@Year = YEAR(GETDATE()) AND @Month >= MONTH(GETDATE())))
                    BEGIN
                        -- Actualizamos concepto si ya existe
                        UPDATE cash_flow_items
                        SET comment = @Comment,
                            movement_date = DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END)
                        WHERE description = @Desc
                        AND (year > @Year OR (year = @Year AND month > @Month));

                        -- 3. Si lo creamos en los meses futuros, también le pasamos la última posición (@NewDisplayOrder)
                        INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order)
                        SELECT DISTINCT 
                            month, 
                            year, 
                            DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END), 
                            @Desc, @Comment, 0, 0, 0, 0, 0, @NewDisplayOrder
                        FROM cash_flow_items
                        WHERE (year > @Year OR (year = @Year AND month > @Month))
                        AND NOT EXISTS (SELECT 1 FROM cash_flow_items c2 WHERE c2.description = @Desc AND c2.month = cash_flow_items.month AND c2.year = cash_flow_items.year)
                    END

                    SELECT Id FROM @InsertedId;";
            }
            else
            {
                query = @"
                    DECLARE @CurrentDisplayOrder INT;
                    
                    -- Buscamos qué posición tiene el concepto que estamos editando
                    SELECT @CurrentDisplayOrder = display_order FROM cash_flow_items WHERE item_id = @Id;

                    UPDATE cash_flow_items 
                    SET movement_date = @Date, description = @Desc, comment = @Comment, amount_depo = @Depo, amount_casa = @Casa, is_paid = @IsPaid, amount_retiros = @Retiros, amount_extras = @Extras
                    WHERE item_id = @Id;

                    -- REPLICACIÓN AL FUTURO
                    IF (@Year > YEAR(GETDATE()) OR (@Year = YEAR(GETDATE()) AND @Month >= MONTH(GETDATE())))
                    BEGIN
                        -- Actualizamos concepto si ya existe
                        UPDATE cash_flow_items
                        SET comment = @Comment,
                            movement_date = DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END)
                        WHERE description = @Desc
                        AND (year > @Year OR (year = @Year AND month > @Month));

                        -- Si al editar este mes provocamos que se cree en meses futuros, le heredamos la posición actual
                        INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order)
                        SELECT DISTINCT 
                            month, 
                            year, 
                            DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END), 
                            @Desc, @Comment, 0, 0, 0, 0, 0, @CurrentDisplayOrder
                        FROM cash_flow_items
                        WHERE (year > @Year OR (year = @Year AND month > @Month))
                        AND NOT EXISTS (SELECT 1 FROM cash_flow_items c2 WHERE c2.description = @Desc AND c2.month = cash_flow_items.month AND c2.year = cash_flow_items.year)
                    END

                    SELECT @Id;";
            }

            var parameters = new[] {
                new SqlParameter("@Id", item.Id),
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year),
                new SqlParameter("@Date", item.Date),
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
                        
                        -- Obtenemos el nombre del concepto usando el ID que mandó el front
                        SELECT @Desc = description 
                        FROM cash_flow_items 
                        WHERE item_id = @Id;
                        
                        -- Si encontró el nombre, actualizamos toda la historia y el futuro
                        IF @Desc IS NOT NULL AND @Desc <> ''
                        BEGIN
                            UPDATE cash_flow_items 
                            SET display_order = @DisplayOrder 
                            WHERE description = @Desc;
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
    }
}