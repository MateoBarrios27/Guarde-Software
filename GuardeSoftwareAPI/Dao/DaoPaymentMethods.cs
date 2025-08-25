using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoPaymentMethods
	{
        private readonly AccessDB accessDB;

        public DaoPaymentMethods(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetPaymentMethods()
        {
            string query = "SELECT payment_method_id, name FROM payment_methods WHERE active = 1";

            return accessDB.GetTable("payment_methods", query);
        }
    }
}
