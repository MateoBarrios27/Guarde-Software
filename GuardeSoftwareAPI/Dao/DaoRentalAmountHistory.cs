using System;
using System.Data;
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

        public DataTable GetRentalAmountHistoriesList()
        {
            string query = "SELECT rental_amount_history_id, rental_id, amount, start_date, end_date FROM rental_amount_history";

            return accessDB.GetTable("rental_amount_history", query);
        }

        public DataTable GetRentalAmountHistoryByRentalId(int rentalId)
        {

            string query = "SELECT rental_amount_history_id, rental_id, amount, start_date, end_date FROM rental_amount_history WHERE rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@rental_id", SqlDbType.Int){Value  = rentalId},
            };

            return accessDB.GetTable("rental_amount_history", query, parameters);
        }

        public bool CreateRentalAmountHistory(RentalAmountHistory rentalAmountHistory)
        {
            string query = "INSERT INTO rental_amount_history (rental_id, amount, start_date) VALUES (@rental_id, @amount, @start_date)";

            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@rental_id", SqlDbType.Int){Value  = rentalAmountHistory.RentalId},
                new SqlParameter("@amount", SqlDbType.Decimal){Value  = rentalAmountHistory.Amount},
                new SqlParameter("@start_date", SqlDbType.DateTime){Value  = rentalAmountHistory.StartDate},
            };
            return accessDB.ExecuteCommand(query, parameters) > 0;
        }
    }
}
