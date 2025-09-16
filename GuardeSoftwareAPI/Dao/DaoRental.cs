using System;
using System.Data;
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

        public DataTable GetRentals()
        {
            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3 FROM rentals WHERE active = 1";

            return accessDB.GetTable("rentals", query);
        }

        public DataTable GetRentalById(int rentalId)
        {

            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3 FROM rentals WHERE active = 1 AND rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@rental_id", SqlDbType.Int){Value  = rentalId},
            };

            return accessDB.GetTable("rentals", query, parameters);
        }

        public DataTable GetRentalsByClientId(int clientId)
        {

            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3 FROM rentals WHERE active = 1 AND client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return accessDB.GetTable("rentals", query, parameters);
        }

        public bool CreateRental(Rental rental)
        {

            string query = "INSERT INTO rentals (client_id, start_date, contracted_m3) VALUES (@client_id, @start_date, @contracted_m3)";
            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@client_id", SqlDbType.Int){Value  = rental.ClientId},
                new SqlParameter("@start_date", SqlDbType.DateTime){Value  = rental.StartDate},
                new SqlParameter("@contracted_m3", SqlDbType.Int){Value  = rental.ContractedM3},
            };
            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

        public bool DeleteRental(int rentalId)
        {

            string query = "UPDATE rentals SET active = 0 WHERE rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@rental_id", SqlDbType.Int){Value = rentalId},
            };

            return accessDB.ExecuteCommand(query, parameters) > 0;
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

        public async Task<decimal> GetCurrentRentAmountAsync(int rentalId)
        {
            string query = @"
                SELECT amount 
                FROM rental_amount_history
                WHERE rental_id = @rentalId
                  AND GETDATE() BETWEEN start_date AND ISNULL(end_date, '9999-12-31');";

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@rentalId", rentalId)
                };

                object result = await accessDB.ExecuteScalarAsync(query, parameters);

                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToDecimal(result);
                }

                return 0; // If not found amount, return 0
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting rental amount from rental {rentalId}", ex);
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
            };

            string query = @"
                            INSERT INTO rentals(client_id, start_date, contracted_m3)
                            OUTPUT INSERTED.rental_id
                            VALUES(@client_id, @start_date, @contracted_m3);";

            object result = await accessDB.ExecuteScalarAsync(query, parameters);

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("The newly added Rental id could not be returned.");

            return Convert.ToInt32(result);
        }


        //METHOD FOR TRANSACTION
        public async Task<int> CreateRentalTransactionAsync(Rental rental, SqlConnection connection, SqlTransaction transaction)
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
            };

            string query = @"
                            INSERT INTO rentals(client_id, start_date, contracted_m3)
                            OUTPUT INSERTED.rental_id
                            VALUES(@client_id, @start_date, @contracted_m3);";

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
    }
}
