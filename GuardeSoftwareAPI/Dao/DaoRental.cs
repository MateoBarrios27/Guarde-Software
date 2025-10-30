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

        public async Task<decimal> GetCurrentRentAmountAsync(int rentalId, SqlConnection connection)
        {
            string query = @"
                SELECT amount 
                FROM rental_amount_history
                WHERE rental_id = @rentalId
                  AND GETDATE() BETWEEN start_date AND ISNULL(end_date, '9999-12-31');";

            // NO usamos accessDB.ExecuteScalarAsync
            using (var command = new SqlCommand(query, connection))
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
                new SqlParameter("@contracted_m3", SqlDbType.Decimal) // Cambiado a Decimal
                {
                    Precision = 10, Scale = 2, // Ajusta precisión y escala si es necesario
                    Value = rental.ContractedM3.HasValue ? (object)rental.ContractedM3.Value : DBNull.Value
                },
                new SqlParameter("@months_unpaid", SqlDbType.Int) { Value = rental.MonthsUnpaid },
                new SqlParameter("@price_lock_end_date", SqlDbType.Date) { Value = rental.PriceLockEndDate.HasValue ? (object)rental.PriceLockEndDate.Value : DBNull.Value }
            ];

            string query = @"
                INSERT INTO rentals(client_id, start_date, contracted_m3, months_unpaid, price_lock_end_date)
                OUTPUT INSERTED.rental_id
                VALUES(@client_id, @start_date, @contracted_m3, @months_unpaid, @price_lock_end_date);";

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
        public async Task<DataTable> GetRentalsDueForIncreaseAsync()
        {
            // This query finds rentals that need an increase today based on their last increase 
            // date and the client's increase regimen
            string query = @"
                WITH LastIncrease AS (
                    SELECT 
                        rah.rental_id,
                        rah.amount AS current_amount,
                        rah.start_date AS last_increase_date,
                        rah.rental_amount_history_id,
                        -- Usamos ROW_NUMBER para quedarnos solo con el registro más reciente
                        ROW_NUMBER() OVER(PARTITION BY rah.rental_id ORDER BY rah.start_date DESC) as rn
                    FROM rental_amount_history rah
                    JOIN rentals r ON rah.rental_id = r.rental_id
                    WHERE r.active = 1
                )
                SELECT 
                    li.rental_id,
                    li.current_amount,
                    li.last_increase_date,
                    li.rental_amount_history_id,
                    ir.percentage
                FROM LastIncrease li
                JOIN clients c ON (SELECT client_id FROM rentals WHERE rental_id = li.rental_id) = c.client_id
                JOIN clients_x_increase_regimens cxir ON c.client_id = cxir.client_id
                JOIN increase_regimens ir ON cxir.regimen_id = ir.regimen_id
                WHERE 
                    li.rn = 1 -- Nos aseguramos de tomar el último registro de historial
                    AND cxir.end_date IS NULL -- El régimen debe estar activo
                    AND GETDATE() >= DATEADD(month, ir.frequency, li.last_increase_date);";

            return await accessDB.GetTableAsync("rentals_to_update", query);
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
            WITH CurrentRentalAmount AS (
                SELECT rental_id, amount as CurrentRent
                FROM (
                    SELECT rental_id, amount, ROW_NUMBER() OVER(PARTITION BY rental_id ORDER BY start_date DESC) as rn
                    FROM rental_amount_history
                ) as sub
                WHERE rn = 1
            ),
            AccountSummary AS (
                SELECT rental_id, SUM(CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END) AS Balance
                FROM account_movements
                GROUP BY rental_id
            )
            SELECT 
                r.rental_id,
                r.months_unpaid, -- Usamos tu nueva columna
                ISNULL(acc.Balance, 0) AS balance,
                cra.CurrentRent
            FROM rentals r
            LEFT JOIN AccountSummary acc ON r.rental_id = acc.rental_id
            LEFT JOIN CurrentRentalAmount cra ON r.rental_id = cra.rental_id
            WHERE r.active = 1;";

            return await accessDB.GetTableAsync("all_active_rentals", query);
        }

        public async Task IncrementUnpaidMonthsAndApplyInterestAsync(int rentalId, decimal interestAmount, string concept)
        {
            string query = @"
            BEGIN TRANSACTION;
            
            UPDATE rentals 
            SET months_unpaid = months_unpaid + 1 
            WHERE rental_id = @rental_id;
            
            INSERT INTO account_movements (rental_id, movement_date, movement_type, concept, amount)
            VALUES (@rental_id, GETDATE(), 'DEBITO', @concept, @amount);
            
            COMMIT TRANSACTION;";

            var parameters = new SqlParameter[]
            {
            new("@rental_id", rentalId),
            new("@concept", concept),
            new("@amount", interestAmount)
            };
            await accessDB.ExecuteCommandAsync(query, parameters);
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
                    SELECT rental_id, amount AS CurrentRent
                    FROM (
                        SELECT rental_id, amount,
                            ROW_NUMBER() OVER(PARTITION BY rental_id ORDER BY start_date DESC) AS rn
                        FROM rental_amount_history
                    ) AS sub
                    WHERE rn = 1
                ),
                AccountSummary AS (
                    SELECT rental_id,
                        SUM(CASE WHEN movement_type = 'CREDITO' THEN amount ELSE -amount END) AS Balance
                    FROM account_movements
                    GROUP BY rental_id
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
                    c.first_name AS client_name, 
                    c.payment_identifier, 
                    r.months_unpaid,
                    ISNULL(acc.Balance, 0) AS balance,
                    cra.CurrentRent,
                    ISNULL(ll.LockerIdentifiers, '') AS locker_identifiers
                FROM rentals r
                INNER JOIN clients c ON r.client_id = c.client_id
                LEFT JOIN AccountSummary acc ON r.rental_id = acc.rental_id
                LEFT JOIN CurrentRentalAmount cra ON r.rental_id = cra.rental_id
                LEFT JOIN LockerList ll ON r.rental_id = ll.rental_id
                WHERE r.active = 1
                AND (r.months_unpaid > 0 OR ISNULL(acc.Balance, 0) < 0);";

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
            string query = "SELECT TOP 1 rental_id, client_id, start_date, end_date, contracted_m3, months_unpaid, active, price_lock_end_date FROM rentals WHERE client_id = @client_id AND active = 1 ORDER BY start_date DESC";
            SqlParameter[] parameters = { new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId } };

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow)) // SingleRow es más eficiente
                {
                    if (await reader.ReadAsync())
                    {
                        return new Rental
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("rental_id")),
                            ClientId = reader.GetInt32(reader.GetOrdinal("client_id")),
                            StartDate = reader.GetDateTime(reader.GetOrdinal("start_date")),
                            EndDate = reader.IsDBNull(reader.GetOrdinal("end_date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("end_date")),
                            ContractedM3 = reader.IsDBNull(reader.GetOrdinal("contracted_m3")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("contracted_m3")),
                            MonthsUnpaid = reader.IsDBNull(reader.GetOrdinal("months_unpaid")) ? 0 : reader.GetInt32(reader.GetOrdinal("months_unpaid")),
                            Active = reader.GetBoolean(reader.GetOrdinal("active")),
                            PriceLockEndDate = reader.IsDBNull(reader.GetOrdinal("price_lock_end_date")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("price_lock_end_date")) // <-- Leer la nueva columna
                        };
                    }
                }
            }
            return null;
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
    }
}
