using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoRental
    {
        private readonly AccessDB accessDB;

        public DaoRental(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetRentals()
        {
            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid FROM rentals WHERE active = 1";

            return await accessDB.GetTableAsync("rentals", query);
        }

        public async Task<DataTable> GetRentalById(int rentalId)
        {

            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid FROM rentals WHERE active = 1 AND rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@rental_id", SqlDbType.Int){Value  = rentalId},
            };

            return await accessDB.GetTableAsync("rentals", query, parameters);
        }

        public async Task<DataTable> GetRentalsByClientId(int clientId)
        {

            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid FROM rentals WHERE active = 1 AND client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return await accessDB.GetTableAsync("rentals", query, parameters);
        }

        public async Task<DataTable> GetRentalsByClientIdIncludingInactiveAsync(int clientId)
        {

            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid FROM rentals WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return await accessDB.GetTableAsync("rentals", query, parameters);
        }

        public async Task<bool> CreateRental(Rental rental)
        {

            string query = "INSERT INTO rentals (client_id, start_date, contracted_m3, months_unpaid) VALUES (@client_id, @start_date, @contracted_m3, @months_unpaid)";
            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@client_id", SqlDbType.Int){Value  = rental.ClientId},
                new SqlParameter("@start_date", SqlDbType.DateTime){Value  = rental.StartDate},
                new SqlParameter("@contracted_m3", SqlDbType.Int){Value  = rental.ContractedM3},
                new SqlParameter("@months_unpaid", SqlDbType.Int){Value  = rental.MonthsUnpaid},
            };
            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<bool> DeleteRental(int rentalId)
        {

            string query = "UPDATE rentals SET active = 0 WHERE rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@rental_id", SqlDbType.Int){Value = rentalId},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<List<int>> GetActiveRentalsIdsAsync()
        {
            List<int> idsList = new List<int>();

            string query = "SELECT rental_id FROM rentals WHERE active = 1;";

            try
            {
                DataTable table = await accessDB.GetTableAsync("rentals", query);

                foreach (DataRow row in table.Rows)
                {
                    idsList.Add(Convert.ToInt32(row["rental_id"]));
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting active rentals", ex);
            }

            return idsList;
        }

        public async Task<decimal> GetCurrentRentAmountAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                SELECT amount 
                FROM rental_amount_history
                WHERE rental_id = @rentalId
                  AND GETDATE() BETWEEN start_date AND ISNULL(end_date, '9999-12-31');";

            // ¡Es vital pasarle el objeto transaction al SqlCommand!
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@rentalId", rentalId));
                object result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0m;
            }
        }

        public async Task<decimal> GetBalanceByRentalIdAsync(int rentalId, SqlConnection connection)
        {
            string query = @"
                SELECT ISNULL(SUM(CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END), 0) AS Balance
                FROM account_movements
                WHERE rental_id = @rental_id";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@rental_id", rentalId);
                object result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0m;
            }
        }

        public async Task<int> CreateRentalAsync(Rental rental)
        {
            if (rental == null) throw new ArgumentNullException(nameof(rental));

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int) { Value = rental.ClientId },
                new SqlParameter("@start_date", SqlDbType.DateTime) { Value = rental.StartDate },
                new SqlParameter("@contracted_m3", SqlDbType.Int)
                {
                    Value = rental.ContractedM3.HasValue ? (object)rental.ContractedM3.Value : DBNull.Value
                },
                new SqlParameter("@months_unpaid", SqlDbType.Int) { Value = rental.MonthsUnpaid }
            };

            string query = @"
                            INSERT INTO rentals(client_id, start_date, contracted_m3, months_unpaid)
                            OUTPUT INSERTED.rental_id
                            VALUES(@client_id, @start_date, @contracted_m3, @months_unpaid);";

            object result = await accessDB.ExecuteScalarAsync(query, parameters);

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("The newly added Rental id could not be returned.");

            return Convert.ToInt32(result);
        }


        //METHOD FOR TRANSACTION
        public async Task<int> CreateRentalTransactionAsync(Rental rental, SqlConnection connection, SqlTransaction transaction)
        {
            if (rental == null) throw new ArgumentNullException(nameof(rental));

            SqlParameter[] parameters =
            [
                new SqlParameter("@client_id", SqlDbType.Int) { Value = rental.ClientId },
                new SqlParameter("@start_date", SqlDbType.DateTime) { Value = rental.StartDate },
                new SqlParameter("@contracted_m3", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = (object?)rental.ContractedM3 ?? DBNull.Value },
                new SqlParameter("@months_unpaid", SqlDbType.Int) { Value = rental.MonthsUnpaid },
                new SqlParameter("@price_lock_end_date", SqlDbType.Date) { Value = (object?)rental.PriceLockEndDate ?? DBNull.Value },
                new SqlParameter("@increase_anchor_date", SqlDbType.Date) { Value = (object?)rental.IncreaseAnchorDate ?? DBNull.Value }, // Renombrado
                new SqlParameter("@occupied_spaces", SqlDbType.Int) { Value = rental.OccupiedSpaces }
            ];

            string query = @"
                INSERT INTO rentals(client_id, start_date, contracted_m3, months_unpaid, price_lock_end_date, increase_anchor_date, occupied_spaces)
                OUTPUT INSERTED.rental_id
                VALUES(@client_id, @start_date, @contracted_m3, @months_unpaid, @price_lock_end_date, @increase_anchor_date, @occupied_spaces);";

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                object result = await command.ExecuteScalarAsync() ?? DBNull.Value;

                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException("The newly added Rental id could not be returned.");

                return Convert.ToInt32(result);
            }
        }

        // Method to get rentals that need a rent increase today
        // This method is used in the ApplyRentIncreaseJob
        public async Task<DataTable> GetRentalsDueForIncreaseTodayAsync(DateTime today)
        {
            string query = @"
                WITH LatestHistory AS (
                    SELECT 
                        rental_id, 
                        amount AS CurrentAmount, 
                        start_date AS LastIncreaseDate,
                        rental_amount_history_id AS LastHistoryId,
                        ROW_NUMBER() OVER(
                            PARTITION BY rental_id 
                            ORDER BY start_date DESC, 
                                    CASE WHEN end_date IS NULL THEN 1 ELSE 0 END DESC, 
                                    rental_amount_history_id DESC
                        ) as rn
                    FROM rental_amount_history
                )
                SELECT 
                    r.rental_id,
                    r.start_date,
                    r.price_lock_end_date,
                    r.increase_anchor_date,
                    c.increase_frequency_months,
                    lh.CurrentAmount,
                    lh.LastIncreaseDate,
                    lh.LastHistoryId
                FROM rentals r
                JOIN clients c ON r.client_id = c.client_id
                JOIN LatestHistory lh ON r.rental_id = lh.rental_id
                WHERE r.active = 1
                  AND lh.rn = 1
                  AND r.increase_anchor_date <= @Today
                  AND (r.price_lock_end_date IS NULL OR r.price_lock_end_date < @Today);
            ";
            
            SqlParameter[] parameters = {
                new SqlParameter("@Today", SqlDbType.Date) { Value = today }
            };
            
            return await accessDB.GetTableAsync("rentals_to_increase", query, parameters);
        }


        // Method to apply the rent increase
        public async Task ApplyRentIncreaseAsync(int rentalId, decimal newAmount, int oldHistoryId)
        {
            // We use a transaction to ensure both operations (update old record and insert new record) are atomic
            using (var connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var today = DateTime.Now.Date;

                        // 1. Update the end_date of the old record to yesterday
                        string updateQuery = "UPDATE rental_amount_history SET end_date = @end_date WHERE rental_amount_history_id = @history_id;";
                        var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                        updateCommand.Parameters.AddWithValue("@end_date", today.AddDays(-1));
                        updateCommand.Parameters.AddWithValue("@history_id", oldHistoryId);
                        await updateCommand.ExecuteNonQueryAsync();

                        // 2. Insert the new record with the new amount and today's date as start_date
                        string insertQuery = "INSERT INTO rental_amount_history (rental_id, amount, start_date) VALUES (@rental_id, @amount, @start_date);";
                        var insertCommand = new SqlCommand(insertQuery, connection, transaction);
                        insertCommand.Parameters.AddWithValue("@rental_id", rentalId);
                        insertCommand.Parameters.AddWithValue("@amount", newAmount);
                        insertCommand.Parameters.AddWithValue("@start_date", today);
                        await insertCommand.ExecuteNonQueryAsync();

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }
        
        public async Task<DataTable> GetAllActiveRentalsWithStatusAsync()
        {
            string query = @"
                WITH RankedRentalAmount AS (
                    SELECT 
                        rental_id, 
                        amount AS CurrentRent, 
                        ROW_NUMBER() OVER(
                            PARTITION BY rental_id 
                            ORDER BY start_date DESC, 
                                     CASE WHEN end_date IS NULL THEN 1 ELSE 0 END DESC, 
                                     rental_amount_history_id DESC
                        ) as rn
                    FROM rental_amount_history
                ),
                CurrentRentalAmount AS (
                    SELECT rental_id, CurrentRent
                    FROM RankedRentalAmount
                    WHERE rn = 1
                ),
                LastMonthBalance AS (
                    SELECT 
                        cmb.rental_id,
                        (cmb.balance - cmb.paid - cmb.advanced_payment) AS Debt,
                        ISNULL(cmb.interests, 0) AS interests,
                        ISNULL(cmb.monthly_debits, 0) AS monthly_debits,
                        -- Cascada: saldo anterior -> intereses -> alquiler del mes.
                        CASE 
                            WHEN ISNULL(cmb.monthly_debits, 0) > applied.AppliedToRent
                                THEN ISNULL(cmb.monthly_debits, 0) - applied.AppliedToRent
                            ELSE 0
                        END AS UnpaidMonthlyDebits,
                        ROW_NUMBER() OVER(
                            PARTITION BY cmb.rental_id
                            ORDER BY monthInfo.MonthValue DESC, cmb.id DESC
                        ) as rn
                    FROM client_month_balances cmb
                    CROSS APPLY (
                        SELECT MonthValue = TRY_CONVERT(int, RIGHT(cmb.month_year, 4)) * 100
                                          + TRY_CONVERT(int, LEFT(cmb.month_year, 2))
                    ) monthInfo
                    CROSS APPLY (
                        SELECT AppliedAfterPrevious = CASE
                            WHEN ISNULL(cmb.paid, 0) + ISNULL(cmb.advanced_payment, 0) > ISNULL(cmb.previous_balance, 0)
                                THEN ISNULL(cmb.paid, 0) + ISNULL(cmb.advanced_payment, 0) - ISNULL(cmb.previous_balance, 0)
                            ELSE 0
                        END
                    ) afterPrevious
                    CROSS APPLY (
                        SELECT AppliedToRent = CASE
                            WHEN afterPrevious.AppliedAfterPrevious > ISNULL(cmb.interests, 0)
                                THEN afterPrevious.AppliedAfterPrevious - ISNULL(cmb.interests, 0)
                            ELSE 0
                        END
                    ) applied
                    WHERE monthInfo.MonthValue <= YEAR(GETDATE()) * 100 + MONTH(GETDATE())
                ),
                UnpaidInterests AS (
                    SELECT 
                        cmb.rental_id,
                        ISNULL(SUM(CASE
                            WHEN ISNULL(cmb.interests, 0) > applied.AppliedAfterPrevious
                                THEN ISNULL(cmb.interests, 0) - applied.AppliedAfterPrevious
                            ELSE 0
                        END), 0) AS TotalUnpaidInterests
                    FROM client_month_balances cmb
                    CROSS APPLY (
                        SELECT MonthValue = TRY_CONVERT(int, RIGHT(cmb.month_year, 4)) * 100
                                          + TRY_CONVERT(int, LEFT(cmb.month_year, 2))
                    ) monthInfo
                    CROSS APPLY (
                        SELECT AppliedAfterPrevious = CASE
                            WHEN ISNULL(cmb.paid, 0) + ISNULL(cmb.advanced_payment, 0) > ISNULL(cmb.previous_balance, 0)
                                THEN ISNULL(cmb.paid, 0) + ISNULL(cmb.advanced_payment, 0) - ISNULL(cmb.previous_balance, 0)
                            ELSE 0
                        END
                    ) applied
                    WHERE monthInfo.MonthValue <= YEAR(GETDATE()) * 100 + MONTH(GETDATE())
                    GROUP BY cmb.rental_id
                )
                SELECT 
                    r.rental_id,
                    r.months_unpaid,
                    ISNULL(cmb.Debt, 0) AS balance,
                    ISNULL(cra.CurrentRent, 0) AS CurrentRent,
                    ISNULL(ui.TotalUnpaidInterests, 0) AS CurrentInterests,   
                    ISNULL(cmb.monthly_debits, 0) AS MonthlyDebits,
                    ISNULL(cmb.UnpaidMonthlyDebits, 0) AS UnpaidMonthlyDebits,
                    -- Traemos el nombre del método de pago preferido
                    ISNULL(pm.name, '') AS PreferredPaymentMethod,
                    -- Si ya existe un registro del próximo mes, el cliente ya pagó este mes
                    CASE WHEN EXISTS (
                        SELECT 1 FROM client_month_balances cmb_next
                        WHERE cmb_next.rental_id = r.rental_id
                          AND cmb_next.month_year = FORMAT(DATEADD(month, 1, GETDATE()), 'MM/yyyy')
                    ) THEN 1 ELSE 0 END AS HasNextMonthBalance
                FROM rentals r
                LEFT JOIN clients c ON r.client_id = c.client_id
                LEFT JOIN payment_methods pm ON c.preferred_payment_method_id = pm.payment_method_id
                LEFT JOIN LastMonthBalance cmb ON r.rental_id = cmb.rental_id AND cmb.rn = 1
                LEFT JOIN CurrentRentalAmount cra ON r.rental_id = cra.rental_id
                LEFT JOIN UnpaidInterests ui ON r.rental_id = ui.rental_id
                WHERE r.active = 1;";

            return await accessDB.GetTableAsync("all_active_rentals", query);
        }

        public async Task IncrementUnpaidMonthsAndSaveInterestAsync(
            int rentalId,
            decimal interestAmount,
            decimal lateRentBase,
            DateTime surchargePeriod)
        {
            // Al comenzar el día 11 guardamos tanto el recargo como la porción del alquiler
            // que efectivamente quedó vencida. Esa base no desaparece por un pago posterior.
            string query = @"
                UPDATE rentals 
                SET months_unpaid = months_unpaid + CASE
                        WHEN pending_surcharge_period = @surcharge_period THEN 0
                        ELSE 1
                    END,
                    pending_surcharge = @amount,
                    pending_surcharge_rent_base = @late_rent_base,
                    pending_surcharge_period = @surcharge_period
                WHERE rental_id = @rental_id;";

            var parameters = new SqlParameter[]
            {
                new("@rental_id", rentalId),
                new("@amount", interestAmount),
                new("@late_rent_base", lateRentBase),
                new("@surcharge_period", new DateTime(surchargePeriod.Year, surchargePeriod.Month, 1))
            };
            
            await accessDB.ExecuteCommandAsync(query, parameters);
        }

        public async Task ApplyPendingSurchargesAsync()
        {
            string query = @"
                BEGIN TRANSACTION;
                
                -- 1. Transformamos la bolsa de espera en un débito real
                INSERT INTO account_movements (rental_id, movement_date, movement_type, concept, amount)
                SELECT 
                    rental_id, 
                    DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1),
                    'DEBITO', 
                    'Interés por mora de ' + FORMAT(
                        COALESCE(pending_surcharge_period, DATEADD(month, -1, GETDATE())),
                        'MMMM yyyy',
                        'es-AR'
                    ),
                    pending_surcharge
                FROM rentals
                WHERE pending_surcharge > 0 AND active = 1;

                -- 2. Vaciamos la bolsa a todos
                UPDATE rentals
                SET pending_surcharge = 0,
                    pending_surcharge_rent_base = NULL,
                    pending_surcharge_period = NULL
                WHERE pending_surcharge > 0 AND active = 1;

                COMMIT TRANSACTION;
            ";

            await accessDB.ExecuteCommandAsync(query, null);
        }

        public async Task ResetUnpaidMonthsAsync(int rentalId)
        {
            string query = "UPDATE rentals SET months_unpaid = 0 WHERE rental_id = @rental_id;";
            var parameters = new SqlParameter[] { new SqlParameter("@rental_id", rentalId) };
            await accessDB.ExecuteCommandAsync(query, parameters);
        }

        public async Task<DataTable> GetPendingPaymentsAsync()
        {
            string query = @"
                WITH CurrentRentalAmount AS (
                    SELECT 
                        rental_id, 
                        amount AS CurrentRent
                    FROM rental_amount_history
                    WHERE GETDATE() BETWEEN start_date AND ISNULL(end_date, '9999-12-31')
                ),
                LockerList AS (
                    SELECT 
                        l.rental_id,
                        STRING_AGG(l.identifier, ', ') AS LockerIdentifiers
                    FROM lockers l
                    WHERE l.rental_id IS NOT NULL
                    GROUP BY l.rental_id
                ) 
                SELECT 
                    r.rental_id,
                    r.client_id,
                    r.increase_anchor_date AS IncreaseAnchorDate,
                    c.increase_frequency_months AS IncreaseFrequencyMonths,
                    c.full_name AS client_name,
                    c.payment_identifier, 
                    c.preferred_payment_method_id,
                    r.months_unpaid,
                    r.pending_surcharge AS PendingSurcharge,
                    ISNULL(step1.UI_Balance, 0) AS balance,
                    ISNULL(latest_cmb.PrevBalDB, 0) AS PreviousBalance,
                    ISNULL(step1.UI_CurrentRent, ISNULL(cr.CurrentRent, 0)) AS CurrentRent,
                    ISNULL(ll.LockerIdentifiers, '') AS locker_identifiers,
                    ISNULL(step1.UI_InterestAmount, 0) AS InterestAmount,
                    latest_cmb.MonthYearDB AS LastGeneratedMonthYear,
                    step1.LastBalanceDate AS NextPaymentDay
                FROM rentals r
                INNER JOIN clients c ON r.client_id = c.client_id
                LEFT JOIN CurrentRentalAmount cr ON r.rental_id = cr.rental_id
                LEFT JOIN LockerList ll ON r.rental_id = ll.rental_id
                OUTER APPLY (
                    SELECT TOP 1
                        PrevBalDB = ISNULL(cmb.previous_balance, 0),
                        MonthYearDB = cmb.month_year,
                        NetBalance = cmb.balance - cmb.paid - cmb.advanced_payment
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                    ORDER BY cmb.id DESC
                ) latest_cmb
                OUTER APPLY (
                    SELECT TOP 1
                        Id = cmb.id,
                        MonthYearDB = cmb.month_year,
                        RentDB = ISNULL(cmb.monthly_debits, ISNULL(cr.CurrentRent, 0)),
                        BalDB = ISNULL(cmb.balance, ISNULL(cr.CurrentRent, 0)),
                        PaidDB = ISNULL(cmb.paid, 0),
                        AdvPayDB = ISNULL(cmb.advanced_payment, 0)
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                    ORDER BY
                        CASE WHEN (cmb.balance - cmb.paid - cmb.advanced_payment) > 0 THEN 0 ELSE 1 END ASC,
                        CASE WHEN (cmb.balance - cmb.paid - cmb.advanced_payment) > 0 THEN cmb.id ELSE -cmb.id END ASC
                ) db
                OUTER APPLY (
                    SELECT 
                        Raw_Interest = ISNULL((
                            SELECT SUM(CASE
                                WHEN ISNULL(cmb2.interests, 0) > CASE
                                    WHEN ISNULL(cmb2.paid, 0) + ISNULL(cmb2.advanced_payment, 0) > ISNULL(cmb2.previous_balance, 0)
                                    THEN ISNULL(cmb2.paid, 0) + ISNULL(cmb2.advanced_payment, 0) - ISNULL(cmb2.previous_balance, 0)
                                    ELSE 0
                                END
                                THEN ISNULL(cmb2.interests, 0) - CASE
                                    WHEN ISNULL(cmb2.paid, 0) + ISNULL(cmb2.advanced_payment, 0) > ISNULL(cmb2.previous_balance, 0)
                                    THEN ISNULL(cmb2.paid, 0) + ISNULL(cmb2.advanced_payment, 0) - ISNULL(cmb2.previous_balance, 0)
                                    ELSE 0
                                END
                                ELSE 0
                            END)
                            FROM client_month_balances cmb2
                            WHERE cmb2.rental_id = r.rental_id AND (cmb2.balance - cmb2.paid - cmb2.advanced_payment) > 0
                        ), 0)
                ) rawData
                OUTER APPLY (
                    SELECT
                        UI_CurrentRent = ISNULL(db.RentDB, ISNULL(cr.CurrentRent, 0)),
                        UI_Balance = -(
                            (ISNULL(db.BalDB, ISNULL(cr.CurrentRent, 0)) - ISNULL(db.PaidDB, 0) - ISNULL(db.AdvPayDB, 0))
                            + CASE
                                WHEN (ISNULL(db.BalDB, ISNULL(cr.CurrentRent, 0)) - ISNULL(db.PaidDB, 0) - ISNULL(db.AdvPayDB, 0)) > 0
                                    THEN ISNULL(r.pending_surcharge, 0)
                                ELSE 0
                              END
                        ),
                        UI_InterestAmount = rawData.Raw_Interest,
                        LastBalanceDate = CASE 
                            WHEN latest_cmb.MonthYearDB IS NOT NULL AND LEN(latest_cmb.MonthYearDB) = 7 THEN
                                CASE 
                                    WHEN latest_cmb.NetBalance <= 0 THEN 
                                        DATEADD(month, 1, DATEFROMPARTS(CAST(RIGHT(latest_cmb.MonthYearDB, 4) AS INT), CAST(LEFT(latest_cmb.MonthYearDB, 2) AS INT), 1))
                                    ELSE
                                        DATEFROMPARTS(CAST(RIGHT(ISNULL(db.MonthYearDB, latest_cmb.MonthYearDB), 4) AS INT), CAST(LEFT(ISNULL(db.MonthYearDB, latest_cmb.MonthYearDB), 2) AS INT), 1)
                                END
                            ELSE NULL 
                        END
                ) step1
                WHERE r.active = 1
                AND (r.months_unpaid > 0 OR ISNULL(step1.UI_Balance, 0) < 0);";

            return await accessDB.GetTableAsync("pending_rentals", query);
        }
        
        //Obstains the balance of a rental inside a transaction
        public async Task<decimal> GetBalanceByRentalIdTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                SELECT ISNULL(SUM(CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END), 0) AS Balance
                FROM account_movements
                WHERE rental_id = @rental_id";

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@rental_id", rentalId);
                object result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToDecimal(result);
                }
                return 0; // If not found amount, return 0
            }
        }

        //Resets the unpaid months of a rental inside a transaction
        public async Task ResetUnpaidMonthsTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "UPDATE rentals SET months_unpaid = 0 WHERE rental_id = @rental_id;";
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@rental_id", rentalId);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<Rental?> GetRentalByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            // CORRECCIÓN: Se agregó 'increase_anchor_date' a la consulta SQL
            string query = "SELECT TOP 1 rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active, price_lock_end_date, occupied_spaces, increase_anchor_date, pending_surcharge, pending_surcharge_rent_base, pending_surcharge_period FROM rentals WHERE client_id = @client_id AND active = 1 ORDER BY start_date DESC";
            SqlParameter[] parameters = [new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId }];

            Rental rental = null;

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                
                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow))
                {
                    if (await reader.ReadAsync())
                    {
                        rental = new Rental
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("rental_id")),
                            ClientId = reader.GetInt32(reader.GetOrdinal("client_id")),
                            StartDate = reader.GetDateTime(reader.GetOrdinal("start_date")),
                            EndDate = reader.IsDBNull(reader.GetOrdinal("end_date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("end_date")),
                            ContractedM3 = reader.IsDBNull(reader.GetOrdinal("contracted_m3")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("contracted_m3")),
                            MonthsUnpaid = reader.IsDBNull(reader.GetOrdinal("months_unpaid")) ? 0 : reader.GetInt32(reader.GetOrdinal("months_unpaid")),
                            Active = reader.GetBoolean(reader.GetOrdinal("active")),
                            PriceLockEndDate = reader.IsDBNull(reader.GetOrdinal("price_lock_end_date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("price_lock_end_date")),
                            OccupiedSpaces = reader.IsDBNull(reader.GetOrdinal("occupied_spaces")) ? 0 : reader.GetInt32(reader.GetOrdinal("occupied_spaces")),
                            IncreaseAnchorDate = reader.IsDBNull(reader.GetOrdinal("increase_anchor_date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("increase_anchor_date")),
                            PendingSurcharge = reader.IsDBNull(reader.GetOrdinal("pending_surcharge")) ? 0 : reader.GetDecimal(reader.GetOrdinal("pending_surcharge")),
                            PendingSurchargeRentBase = reader.IsDBNull(reader.GetOrdinal("pending_surcharge_rent_base")) ? null : reader.GetDecimal(reader.GetOrdinal("pending_surcharge_rent_base")),
                            PendingSurchargePeriod = reader.IsDBNull(reader.GetOrdinal("pending_surcharge_period")) ? null : reader.GetDateTime(reader.GetOrdinal("pending_surcharge_period"))
                        };
                    }
                } 
            }

            if (rental != null)
            {
                rental.CurrentAmount = await GetCurrentRentAmountAsync(rental.Id, connection, transaction);
            }

            return rental;
        }

        public async Task ResetPendingSurchargeTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                UPDATE rentals
                SET pending_surcharge = 0,
                    pending_surcharge_rent_base = NULL,
                    pending_surcharge_period = NULL
                WHERE rental_id = @rental_id;";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddWithValue("@rental_id", rentalId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task SetPendingSurchargeTransactionAsync(
            int rentalId,
            decimal amount,
            decimal lateRentBase,
            DateTime surchargePeriod,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string query = @"
                UPDATE rentals
                SET pending_surcharge = @amount,
                    pending_surcharge_rent_base = @late_rent_base,
                    pending_surcharge_period = @surcharge_period
                WHERE rental_id = @rental_id;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@amount", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = amount });
            command.Parameters.Add(new SqlParameter("@late_rent_base", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = lateRentBase });
            command.Parameters.Add(new SqlParameter("@surcharge_period", SqlDbType.Date) { Value = new DateTime(surchargePeriod.Year, surchargePeriod.Month, 1) });
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> UpdateContractedM3TransactionAsync(int rentalId, decimal newM3, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "UPDATE rentals SET contracted_m3 = @contracted_m3 WHERE rental_id = @rental_id";
            SqlParameter[] parameters =
            {
                new SqlParameter("@contracted_m3", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = newM3 },
                new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId }
            };

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                int rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        public async Task<Rental?> GetActiveRentalByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "SELECT TOP 1 * FROM rentals WHERE client_id = @client_id AND active = 1 ORDER BY start_date DESC";

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId });

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Rental
                        {
                            Id = Convert.ToInt32(reader["rental_id"]),
                            ClientId = Convert.ToInt32(reader["client_id"]),
                            StartDate = Convert.ToDateTime(reader["start_date"]),
                            EndDate = reader["end_date"] != DBNull.Value ? Convert.ToDateTime(reader["end_date"]) : null,
                            ContractedM3 = reader["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(reader["contracted_m3"]) : null,
                            MonthsUnpaid = Convert.ToInt32(reader["months_unpaid"]),
                            Active = Convert.ToBoolean(reader["active"]),
                            PriceLockEndDate = reader["price_lock_end_date"] != DBNull.Value ? Convert.ToDateTime(reader["price_lock_end_date"]) : null,
                            IncreaseAnchorDate = reader["increase_anchor_date"] != DBNull.Value ? Convert.ToDateTime(reader["increase_anchor_date"]) : null,
                            PendingSurcharge = reader["pending_surcharge"] != DBNull.Value ? Convert.ToDecimal(reader["pending_surcharge"]) : 0m,
                            PendingSurchargeRentBase = reader["pending_surcharge_rent_base"] != DBNull.Value ? Convert.ToDecimal(reader["pending_surcharge_rent_base"]) : null,
                            PendingSurchargePeriod = reader["pending_surcharge_period"] != DBNull.Value ? Convert.ToDateTime(reader["pending_surcharge_period"]) : null
                        };
                    }
                }
            }
            return null; // No se encontró rental activo
        }
        
        public async Task<bool> UpdateNextIncreaseDateTransactionAsync(int rentalId, DateTime newNextIncreaseDate, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "UPDATE rentals SET increase_anchor_date = @NewNextIncreaseDate WHERE rental_id = @RentalId";
            SqlParameter[] parameters =
            [
                new("@NewNextIncreaseDate", SqlDbType.Date) { Value = newNextIncreaseDate },
                new("@RentalId", SqlDbType.Int) { Value = rentalId }
            ];

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                int rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        public async Task<bool> EndActiveRentalByClientIdTransactionAsync(int clientId, DateTime endDate, SqlConnection connection, SqlTransaction transaction)
        {
            // Actualiza el rental activo: lo desactiva y pone fecha de fin
            string query = @"
                UPDATE rentals 
                SET active = 0, end_date = @EndDate 
                WHERE client_id = @ClientId AND active = 1";

            SqlParameter[] parameters = {
                new("@ClientId", SqlDbType.Int) { Value = clientId },
                new("@EndDate", SqlDbType.Date) { Value = endDate }
            };

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            // No verificamos rows > 0 porque puede que el cliente no tenga rental activo y eso no es un error para darlo de baja
            await command.ExecuteNonQueryAsync();
            return true;
        }
        
        // Necesitas obtener el ID del rental activo para cerrar su historial
        public async Task<int?> GetActiveRentalIdByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
             string query = "SELECT rental_id FROM rentals WHERE client_id = @ClientId AND active = 1";
             SqlParameter[] parameters = { new SqlParameter("@ClientId", SqlDbType.Int) { Value = clientId } };

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            object result = await command.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? (int)result : null;
        }

        public async Task<bool> UpdateOccupiedSpacesTransactionAsync(int rentalId, int spaces, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "UPDATE rentals SET occupied_spaces = @Spaces WHERE rental_id = @RentalId";
            SqlParameter[] parameters = [
                new("@Spaces", SqlDbType.Int) { Value = spaces },
                new("@RentalId", SqlDbType.Int) { Value = rentalId }
            ];

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                int rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }

        public async Task UpdatePriceLockEndDateTransactionAsync(int rentalId, DateTime priceLockEndDate, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                UPDATE rentals
                SET price_lock_end_date = @price_lock_end_date
                WHERE rental_id = @rental_id;
            ";

            SqlParameter[] parameters =
            [
                new SqlParameter("@price_lock_end_date", SqlDbType.Date) { Value = priceLockEndDate.Date },
                new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId }
            ];

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                int rows = await command.ExecuteNonQueryAsync();

                if (rows == 0)
                    throw new Exception($"No se pudo actualizar price_lock_end_date para rental id = {rentalId}.");
            }
        }

        public async Task<bool> UpdateIncreaseAnchorDateTransactionAsync(int rentalId, DateTime newAnchorDate, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                UPDATE rentals 
                SET increase_anchor_date = @IncreaseAnchorDate 
                WHERE rental_id = @RentalId";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@IncreaseAnchorDate", SqlDbType.Date) { Value = newAnchorDate });
            command.Parameters.Add(new SqlParameter("@RentalId", SqlDbType.Int) { Value = rentalId });

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
    }
}
