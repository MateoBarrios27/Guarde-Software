using System;
using System.Data;
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

        public DataTable GetRentalById(int rentalId) {

            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3 FROM rentals WHERE active = 1 AND rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@rental_id", SqlDbType.Int){Value  = rentalId},
            };

            return accessDB.GetTable("rentals", query, parameters);
        }

        public DataTable GetRentalsByClientId(int clientId) {

            string query = "SELECT rental_id, client_id, start_date, end_date, contracted_m3 FROM rentals WHERE active = 1 AND client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return accessDB.GetTable("rentals", query, parameters);
        }

        public bool DeleteRental(int rentalId) {

            string query = "UPDATE rentals SET active = 0 WHERE rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@rental_id", SqlDbType.Int){Value = rentalId},
            };

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }
    }
}
