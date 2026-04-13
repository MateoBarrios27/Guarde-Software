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
                SELECT item_id, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order, replication_state
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
                    DisplayOrder = row["display_order"] != DBNull.Value ? Convert.ToInt32(row["display_order"]) : 0,
                    ReplicationState = row["replication_state"] != DBNull.Value ? Convert.ToInt32(row["replication_state"]) : 0
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

                    INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order, replication_state)
                    OUTPUT INSERTED.item_id INTO @InsertedId
                    VALUES (@Month, @Year, @Date, @Desc, @Comment, @Depo, @Casa, @IsPaid, @Retiros, @Extras, @NewDisplayOrder, @RepState);

                    IF @RepState IN (1, 2)
                    BEGIN
                        UPDATE cash_flow_items
                        SET comment = @Comment,
                            replication_state = @RepState,
                            movement_date = CASE WHEN @RepState = 2 THEN CASE WHEN @Date IS NULL THEN NULL ELSE DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END) END ELSE NULL END,
                            amount_depo = CASE WHEN @RepState = 2 THEN @Depo ELSE 0 END,
                            amount_casa = CASE WHEN @RepState = 2 THEN @Casa ELSE 0 END,
                            amount_retiros = CASE WHEN @RepState = 2 THEN @Retiros ELSE 0 END,
                            amount_extras = CASE WHEN @RepState = 2 THEN @Extras ELSE 0 END
                        WHERE description = @Desc
                        AND (year > @Year OR (year = @Year AND month > @Month));

                        INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order, replication_state)
                        SELECT DISTINCT 
                            month, 
                            year, 
                            CASE WHEN @RepState = 2 THEN CASE WHEN @Date IS NULL THEN NULL ELSE DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END) END ELSE NULL END,
                            @Desc, 
                            @Comment, 
                            CASE WHEN @RepState = 2 THEN @Depo ELSE 0 END, 
                            CASE WHEN @RepState = 2 THEN @Casa ELSE 0 END, 
                            0,
                            CASE WHEN @RepState = 2 THEN @Retiros ELSE 0 END, 
                            CASE WHEN @RepState = 2 THEN @Extras ELSE 0 END, 
                            @NewDisplayOrder, 
                            @RepState
                        FROM cash_flow_items
                        WHERE (year > @Year OR (year = @Year AND month > @Month))
                        AND NOT EXISTS (SELECT 1 FROM cash_flow_items c2 WHERE c2.description = @Desc AND c2.month = cash_flow_items.month AND c2.year = cash_flow_items.year);
                    END

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
                    SET movement_date = @Date, description = @Desc, comment = @Comment, amount_depo = @Depo, amount_casa = @Casa, is_paid = @IsPaid, amount_retiros = @Retiros, amount_extras = @Extras, replication_state = @RepState
                    WHERE item_id = @Id;

                    IF @RepState IN (1, 2)
                    BEGIN
                        UPDATE cash_flow_items
                        SET description = @Desc,
                            comment = @Comment,
                            replication_state = @RepState,
                            movement_date = CASE WHEN @RepState = 2 THEN CASE WHEN @Date IS NULL THEN NULL ELSE DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END) END ELSE NULL END,
                            amount_depo = CASE WHEN @RepState = 2 THEN @Depo ELSE 0 END,
                            amount_casa = CASE WHEN @RepState = 2 THEN @Casa ELSE 0 END,
                            amount_retiros = CASE WHEN @RepState = 2 THEN @Retiros ELSE 0 END,
                            amount_extras = CASE WHEN @RepState = 2 THEN @Extras ELSE 0 END
                        WHERE description = @OldDesc
                        AND (year > @Year OR (year = @Year AND month > @Month));

                        INSERT INTO cash_flow_items (month, year, movement_date, description, comment, amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, display_order, replication_state)
                        SELECT DISTINCT 
                            month, 
                            year, 
                            CASE WHEN @RepState = 2 THEN CASE WHEN @Date IS NULL THEN NULL ELSE DATEFROMPARTS(year, month, CASE WHEN DAY(@Date) > DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(year, month, 1))) ELSE DAY(@Date) END) END ELSE NULL END,
                            @Desc, 
                            @Comment, 
                            CASE WHEN @RepState = 2 THEN @Depo ELSE 0 END, 
                            CASE WHEN @RepState = 2 THEN @Casa ELSE 0 END, 
                            0,
                            CASE WHEN @RepState = 2 THEN @Retiros ELSE 0 END, 
                            CASE WHEN @RepState = 2 THEN @Extras ELSE 0 END, 
                            @CurrentDisplayOrder, 
                            @RepState
                        FROM cash_flow_items
                        WHERE (year > @Year OR (year = @Year AND month > @Month))
                        AND NOT EXISTS (SELECT 1 FROM cash_flow_items c2 WHERE c2.description = @Desc AND c2.month = cash_flow_items.month AND c2.year = cash_flow_items.year);
                    END
                    ELSE IF @RepState = 0
                    BEGIN
                        DELETE FROM cash_flow_items 
                        WHERE description = @OldDesc 
                        AND (year > @Year OR (year = @Year AND month > @Month));
                    END

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
                new SqlParameter("@Extras", item.Extras),
                new SqlParameter("@RepState", item.ReplicationState) 
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

                IF (@ItemYear > YEAR(GETDATE()) OR (@ItemYear = YEAR(GETDATE()) AND @ItemMonth >= MONTH(GETDATE())))
                BEGIN
                    DELETE FROM cash_flow_items 
                    WHERE description = @Desc 
                    AND (year > @ItemYear OR (year = @ItemYear AND month > @ItemMonth))
                END";

            await _accessDB.ExecuteCommandAsync(query, [new SqlParameter("@Id", id)]);
        }

        #endregion

        #region 2. Cuentas Financieras

        public async Task<List<FinancialAccountDto>> GetAccountsAsync(int month, int year)
        {
            var list = new List<FinancialAccountDto>();
            string query = @"
                SELECT account_id, name, type, current_balance, currency, display_order, color 
                FROM financial_accounts 
                WHERE month = @Month AND year = @Year
                ORDER BY display_order ASC, name ASC";

            var parameters = new[] {
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            };

            var dt = await _accessDB.GetTableAsync("Accounts", query, parameters);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new FinancialAccountDto
                {
                    Id = Convert.ToInt32(row["account_id"]),
                    Name = row["name"].ToString(),
                    Type = row["type"].ToString(),
                    Currency = row["currency"].ToString(),
                    Balance = Convert.ToDecimal(row["current_balance"]),
                    Color = row["color"] != DBNull.Value ? row["color"].ToString() : null,
                });
            }
            return list;
        }

        public async Task UpdateAccountBalanceAsync(int id, decimal balance)
        {
            string query = @"
                DECLARE @AccMonth INT;
                DECLARE @AccYear INT;
                DECLARE @AccName NVARCHAR(255);

                SELECT @AccMonth = month, @AccYear = year, @AccName = name 
                FROM financial_accounts WHERE account_id = @Id;

                UPDATE financial_accounts SET current_balance = @Balance, last_updated = GETDATE() WHERE account_id = @Id;

                IF (@AccYear > YEAR(GETDATE()) OR (@AccYear = YEAR(GETDATE()) AND @AccMonth >= MONTH(GETDATE())))
                BEGIN
                    UPDATE financial_accounts SET current_balance = @Balance, last_updated = GETDATE()
                    WHERE name = @AccName AND (year > @AccYear OR (year = @AccYear AND month > @AccMonth));
                END";

            var parameters = new[] {
                new SqlParameter("@Balance", balance),
                new SqlParameter("@Id", id)
            };
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        #endregion

        #region 3. Cálculos para Resumen (Automático)
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


        public async Task<decimal> GetPendingCollectionAsync(int month, int year)
        {
            DateTime startDate = new(year, month, 1);
            SqlParameter[] parameters = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate }
            ];

            string query = "SELECT ISNULL(SUM( CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END ), 0) FROM account_movements WHERE movement_date < @StartDate";
            
            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToDecimal(result);
        }

        public async Task<decimal> GetManualExpensesTotalAsync(int month, int year)
        {
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

            string queryItems = @"
                INSERT INTO cash_flow_items (
                    month, year, movement_date, description, comment, 
                    amount_depo, amount_casa, is_paid, amount_retiros, amount_extras, 
                    display_order, replication_state
                )
                SELECT 
                    @CurrentMonth, @CurrentYear, 
                    
                    CASE 
                        WHEN replication_state = 2 THEN 
                            CASE WHEN movement_date IS NULL THEN NULL 
                            ELSE DATEFROMPARTS(@CurrentYear, @CurrentMonth, CASE WHEN DAY(movement_date) > DAY(EOMONTH(DATEFROMPARTS(@CurrentYear, @CurrentMonth, 1))) THEN DAY(EOMONTH(DATEFROMPARTS(@CurrentYear, @CurrentMonth, 1))) ELSE DAY(movement_date) END) END
                        ELSE NULL 
                    END, 
                    
                    description, 
                    comment, 
                    
                    CASE WHEN replication_state = 2 THEN amount_depo ELSE 0 END, 
                    CASE WHEN replication_state = 2 THEN amount_casa ELSE 0 END, 
                    
                    0,
                    
                    CASE WHEN replication_state = 2 THEN amount_retiros ELSE 0 END, 
                    CASE WHEN replication_state = 2 THEN amount_extras ELSE 0 END, 
                    
                    display_order,
                    replication_state
                FROM cash_flow_items
                WHERE month = @PrevMonth AND year = @PrevYear
                AND replication_state IN (1, 2)
                AND NOT EXISTS (
                    SELECT 1 FROM cash_flow_items c2 
                    WHERE c2.description = cash_flow_items.description 
                    AND c2.month = @CurrentMonth AND c2.year = @CurrentYear
                )";

            string queryAccounts = @"
                INSERT INTO financial_accounts (
                    name, type, current_balance, currency, display_order, month, year, color
                )
                SELECT 
                    name, type, current_balance, currency, display_order, @CurrentMonth, @CurrentYear, color
                FROM financial_accounts
                WHERE month = @PrevMonth AND year = @PrevYear
                AND NOT EXISTS (
                    SELECT 1 FROM financial_accounts f2 
                    WHERE f2.name = financial_accounts.name 
                    AND f2.month = @CurrentMonth AND f2.year = @CurrentYear
                )";

            string querySettings = @"
                INSERT INTO monthly_cash_settings (month, year, usd_exchange_rate)
                SELECT @CurrentMonth, @CurrentYear, usd_exchange_rate
                FROM monthly_cash_settings
                WHERE month = @PrevMonth AND year = @PrevYear
                AND NOT EXISTS (
                    SELECT 1 FROM monthly_cash_settings 
                    WHERE month = @CurrentMonth AND year = @CurrentYear
                )";

            var parametersSettings = new[] {
                new SqlParameter("@CurrentMonth", currentMonth),
                new SqlParameter("@CurrentYear", currentYear),
                new SqlParameter("@PrevMonth", prevMonth),
                new SqlParameter("@PrevYear", prevYear)
            };

            var parametersItems = new[] {
                new SqlParameter("@CurrentMonth", currentMonth),
                new SqlParameter("@CurrentYear", currentYear),
                new SqlParameter("@PrevMonth", prevMonth),
                new SqlParameter("@PrevYear", prevYear)
            };

            var parametersAccounts = new[] {
                new SqlParameter("@CurrentMonth", currentMonth),
                new SqlParameter("@CurrentYear", currentYear),
                new SqlParameter("@PrevMonth", prevMonth),
                new SqlParameter("@PrevYear", prevYear)
            };

            await _accessDB.ExecuteCommandAsync(queryItems, parametersItems);
            await _accessDB.ExecuteCommandAsync(queryAccounts, parametersAccounts);
            await _accessDB.ExecuteCommandAsync(querySettings, parametersSettings);
        
        }

        public async Task<int> CreateAccountAsync(FinancialAccountDto account, int month, int year)
        {
            string query = @"
                DECLARE @NewOrder INT;
                SELECT @NewOrder = ISNULL(MAX(display_order), 0) + 1 FROM financial_accounts WHERE month = @Month AND year = @Year;

                INSERT INTO financial_accounts (name, type, current_balance, currency, display_order, month, year, color) 
                VALUES (@Name, @Type, @Balance, @Currency, @NewOrder, @Month, @Year, @Color);

                DECLARE @InsertedId INT = SCOPE_IDENTITY();

                IF (@Year > YEAR(GETDATE()) OR (@Year = YEAR(GETDATE()) AND @Month >= MONTH(GETDATE())))
                BEGIN
                    INSERT INTO financial_accounts (name, type, current_balance, currency, display_order, month, year, color)
                    SELECT DISTINCT @Name, @Type, @Balance, @Currency, @NewOrder, month, year, @Color
                    FROM financial_accounts
                    WHERE (year > @Year OR (year = @Year AND month > @Month))
                    AND NOT EXISTS (
                        SELECT 1 FROM financial_accounts f2 
                        WHERE f2.name = @Name AND f2.month = financial_accounts.month AND f2.year = financial_accounts.year
                    );
                END

                SELECT @InsertedId;";

            var parameters = new[] {
                new SqlParameter("@Name", SqlDbType.NVarChar) { Value = account.Name },
                new SqlParameter("@Type", SqlDbType.NVarChar) { Value = account.Type },
                new SqlParameter("@Balance", SqlDbType.Decimal) { Value = account.Balance != null ? account.Balance : 0 },
                new SqlParameter("@Currency", SqlDbType.NVarChar) { Value = account.Currency ?? "" },
                new SqlParameter("@Color", SqlDbType.VarChar) { Value = !string.IsNullOrEmpty(account.Color) ? account.Color : "#1f2937" },
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            };

            var result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return result != DBNull.Value && result != null ? Convert.ToInt32(result) : 0;
        }

        public async Task DeleteAccountAsync(int id)
        {
            string query = @"
                DECLARE @AccMonth INT;
                DECLARE @AccYear INT;
                DECLARE @AccName NVARCHAR(255);

                SELECT @AccMonth = month, @AccYear = year, @AccName = name 
                FROM financial_accounts WHERE account_id = @Id;

                DELETE FROM financial_accounts WHERE account_id = @Id;

                IF (@AccYear > YEAR(GETDATE()) OR (@AccYear = YEAR(GETDATE()) AND @AccMonth >= MONTH(GETDATE())))
                BEGIN
                    DELETE FROM financial_accounts 
                    WHERE name = @AccName AND (year > @AccYear OR (year = @AccYear AND month > @AccMonth));
                END";
            
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

                MonthlyCredits AS (
                    SELECT 
                        r.client_id,
                        ISNULL(SUM(am.amount), 0) as TotalCreditedInMonth
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE am.movement_type <> 'DEBITO' 
                    AND MONTH(am.movement_date) = @Month
                    AND YEAR(am.movement_date) = @Year
                    GROUP BY r.client_id
                ),

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

                SELECT ISNULL(SUM(
                    CASE 
                        WHEN (mc.TotalCreditedInMonth - ISNULL(md.TotalDebitedInMonth, 0)) > pb.CurrentGlobalBalance 
                        THEN pb.CurrentGlobalBalance
                        
                        ELSE (mc.TotalCreditedInMonth - ISNULL(md.TotalDebitedInMonth, 0))
                    END
                ), 0)
                FROM MonthlyCredits mc
                INNER JOIN PositiveBalances pb ON mc.client_id = pb.client_id
                LEFT JOIN MonthlyDebits md ON mc.client_id = md.client_id
                WHERE 
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
                        
                        SELECT @Desc = description, @ItemMonth = month, @ItemYear = year
                        FROM cash_flow_items 
                        WHERE item_id = @Id;
                        
                        IF @Desc IS NOT NULL AND @Desc <> ''
                        BEGIN
                            UPDATE cash_flow_items 
                            SET display_order = @DisplayOrder 
                            WHERE description = @Desc
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
                    string query = @"
                        DECLARE @AccMonth INT;
                        DECLARE @AccYear INT;
                        DECLARE @AccName NVARCHAR(255);

                        SELECT @AccMonth = month, @AccYear = year, @AccName = name 
                        FROM financial_accounts WHERE account_id = @Id;

                        IF @AccName IS NOT NULL
                        BEGIN
                            UPDATE financial_accounts SET display_order = @DisplayOrder WHERE account_id = @Id;

                            IF (@AccYear > YEAR(GETDATE()) OR (@AccYear = YEAR(GETDATE()) AND @AccMonth >= MONTH(GETDATE())))
                            BEGIN
                                UPDATE financial_accounts SET display_order = @DisplayOrder 
                                WHERE name = @AccName AND (year > @AccYear OR (year = @AccYear AND month > @AccMonth));
                            END
                        END";
                    
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

        public async Task<decimal> GetUsdRateAsync(int month, int year)
        {
            string query = "SELECT usd_exchange_rate FROM monthly_cash_settings WHERE month = @Month AND year = @Year";
            var result = await _accessDB.ExecuteScalarAsync(query, new[] {
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            });
            return result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 1m;
        }

        public async Task UpdateUsdRateAsync(decimal rate, int month, int year)
        {
            string query = @"
                IF EXISTS (SELECT 1 FROM monthly_cash_settings WHERE month = @Month AND year = @Year)
                BEGIN
                    UPDATE monthly_cash_settings SET usd_exchange_rate = @Rate WHERE month = @Month AND year = @Year
                END
                ELSE
                BEGIN
                    INSERT INTO monthly_cash_settings (month, year, usd_exchange_rate) VALUES (@Month, @Year, @Rate)
                END

                IF (@Year > YEAR(GETDATE()) OR (@Year = YEAR(GETDATE()) AND @Month >= MONTH(GETDATE())))
                BEGIN
                    UPDATE monthly_cash_settings 
                    SET usd_exchange_rate = @Rate 
                    WHERE (year > @Year OR (year = @Year AND month > @Month));
                END";
            
            var parameters = new[] {
                new SqlParameter("@Rate", rate),
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            };
            
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        public async Task UpdateAccountColorAsync(int accountId, string color)
        {
            string query = @"
                DECLARE @AccMonth INT;
                DECLARE @AccYear INT;
                DECLARE @AccName NVARCHAR(255);

                SELECT @AccMonth = month, @AccYear = year, @AccName = name 
                FROM financial_accounts WHERE account_id = @AccountId;

                UPDATE financial_accounts SET color = @Color WHERE account_id = @AccountId;

                IF (@AccYear > YEAR(GETDATE()) OR (@AccYear = YEAR(GETDATE()) AND @AccMonth >= MONTH(GETDATE())))
                BEGIN
                    UPDATE financial_accounts 
                    SET color = @Color 
                    WHERE name = @AccName 
                    AND (year > @AccYear OR (year = @AccYear AND month > @AccMonth));
                END";
            
            var parameters = new[] {
                new SqlParameter("@Color", (object)color ?? DBNull.Value),
                new SqlParameter("@AccountId", accountId)
            };
            
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }
    }
}