using System;
using System.Data;


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
            string consult = "SELECT rental_amount_history_id, rental_id, amount, start_date, end_date FROM rental_amount_history";

            return accessDB.GetTable("rental_amount_history", consult);
        }

    }
}
