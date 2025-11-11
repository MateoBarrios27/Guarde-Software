using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;    

namespace GuardeSoftwareAPI.Dao
{
    public class DaoRentalAmountHistory
    {
        private readonly AccessDB accessDB;

        public DaoRentalAmountHistory(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetRentalAmountHistoriesList()
        {
            string query = "SELECT rental_amount_history_id, rental_id, amount, start_date, end_date FROM rental_amount_history";

            return await accessDB.GetTableAsync("rental_amount_history", query);
        }

        public async Task<DataTable> GetRentalAmountHistoryByRentalId(int rentalId)
        {

            string query = "SELECT rental_amount_history_id, rental_id, amount, start_date, end_date FROM rental_amount_history WHERE rental_id = @rental_id";

            SqlParameter[] parameters = [

                new SqlParameter("@rental_id", SqlDbType.Int){Value  = rentalId},
            ];

            return await accessDB.GetTableAsync("rental_amount_history", query, parameters);
        }

        public async Task<RentalAmountHistory> CreateRentalAmountHistory(RentalAmountHistory rentalAmountHistory)
        {
            string query = "INSERT INTO rental_amount_history (rental_id, amount, start_date) VALUES (@rental_id, @amount, @start_date); SELECT SCOPE_IDENTITY()";

            SqlParameter[] parameters = [
                new SqlParameter("@rental_id", SqlDbType.Int){Value  = rentalAmountHistory.RentalId},
                new SqlParameter("@amount", SqlDbType.Decimal){Value  = rentalAmountHistory.Amount},
                new SqlParameter("@start_date", SqlDbType.DateTime){Value  = rentalAmountHistory.StartDate},
            ];
            
            object newId = await accessDB.ExecuteScalarAsync(query, parameters);

            if (newId != null && newId != DBNull.Value)
            {
                //Assign the newly generated ID to the rental amount history object
                rentalAmountHistory.Id = Convert.ToInt32(newId);
            }

            return rentalAmountHistory;
        }

        public async Task<int> CreateRentalAmountHistoryAsync(RentalAmountHistory rentalAmountHistory)
        {
            SqlParameter[] parameters =
            [
                new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalAmountHistory.RentalId },
                new SqlParameter("@amount", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Value = rentalAmountHistory.Amount
                },
                new SqlParameter("@start_date", SqlDbType.DateTime) { Value = rentalAmountHistory.StartDate },
            ];

            string query = @"
                            INSERT INTO rental_amount_history (rental_id, amount, start_date)
                            OUTPUT INSERTED.rental_amount_history_id
                            VALUES (@rental_id, @amount, @start_date);";

            object result = await accessDB.ExecuteScalarAsync(query, parameters);

            return Convert.ToInt32(result);
        }


        //METHOD FOR TRANSACTION
        public async Task<int> CreateRentalAmountHistoryTransactionAsync(RentalAmountHistory rentalAmountHistory, SqlConnection connection, SqlTransaction transaction)
        {
            SqlParameter[] parameters =
            [
                new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalAmountHistory.RentalId },
                new SqlParameter("@amount", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Value = rentalAmountHistory.Amount
                },
                new SqlParameter("@start_date", SqlDbType.Date) { Value = rentalAmountHistory.StartDate }, // Usar Date
                new SqlParameter("@end_date", SqlDbType.Date) { Value = (object?)rentalAmountHistory.EndDate ?? DBNull.Value } // Acepta nulos
            ];

            string query = @"
                INSERT INTO rental_amount_history (rental_id, amount, start_date, end_date)
                OUTPUT INSERTED.rental_amount_history_id
                VALUES (@rental_id, @amount, @start_date, @end_date);"; // Añadido end_date

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                object result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }


        public async Task<RentalAmountHistory?> GetLatestRentalAmountHistoryTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "SELECT TOP 1 * FROM rental_amount_history WHERE rental_id = @rental_id ORDER BY start_date DESC";
            SqlParameter[] parameters = { new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId } };

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new RentalAmountHistory
                        {
                            Id = Convert.ToInt32(reader["rental_amount_history_id"]),
                            RentalId = Convert.ToInt32(reader["rental_id"]),
                            Amount = Convert.ToDecimal(reader["amount"]),
                            StartDate = Convert.ToDateTime(reader["start_date"]),
                            EndDate = reader["end_date"] != DBNull.Value ? Convert.ToDateTime(reader["end_date"]) : null
                        };
                    }
                }
            }
            return null;
        }

        public async Task<bool> EndRentalAmountHistoryTransactionAsync(int historyId, DateTime endDate, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "UPDATE rental_amount_history SET end_date = @end_date WHERE rental_amount_history_id = @history_id AND end_date IS NULL";
            SqlParameter[] parameters =
            {
                new SqlParameter("@end_date", SqlDbType.Date) { Value = endDate },
                new SqlParameter("@history_id", SqlDbType.Int) { Value = historyId }
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
