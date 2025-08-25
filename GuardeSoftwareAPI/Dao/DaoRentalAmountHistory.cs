using System;
using System.Data;
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

        public DataTable GetRentalAmountHistory()
        {
            string query = "SELECT rental_amount_history_id, rental_id, amount, start_date, end_date FROM rental_amount_history";

            return accessDB.GetTable("rental_amount_history", query);
        }

        public DataTable GetRentalAmountHistoryByRentalId(int rentalId) {

            string query = "SELECT rental_amount_history_id, rental_id, amount, start_date, end_date FROM rental_amount_history WHERE rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@rental_id", SqlDbType.Int){Value  = rentalId},
            };

            return accessDB.GetTable("rental_amount_history", query, parameters);

        }
    }
}
