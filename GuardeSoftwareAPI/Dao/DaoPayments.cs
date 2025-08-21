using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoPayments
	{
        private readonly AccessDB accessDB;

        public DaoPayments(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }
        
        public DataTable GetPayments()
        {
            string consult = "SELECT payment_id, customer_id, payment_method_id, payment_date, amount FROM payments";

            return accessDB.GetTable("payments",consult);
        }
    }
}
