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

            string query = "INSERT INTO rentals (client_id, start_date, contracted_m3) VALUES (@client_id, @start_date,, @contracted_m3)";
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
    }
}
