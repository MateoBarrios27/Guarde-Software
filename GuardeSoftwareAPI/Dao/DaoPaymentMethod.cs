using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
	public class DaoPaymentMethod
	{
        private readonly AccessDB accessDB;

        public DaoPaymentMethod(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetPaymentMethods()
        {
            string query = "SELECT payment_method_id, name FROM payment_methods WHERE active = 1";

            return accessDB.GetTable("payment_methods", query);
        }

        public DataTable GetPaymentsMethodsById(int id) { 
        
            string query = "SELECT payment_method_id, name FROM payment_methods WHERE active = 1 AND payment_method_id = @payment_method_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@payment_method_id", SqlDbType.Int){Value  = id},
            };
            return accessDB.GetTable("payment_methods", query, parameters);
        }

        public void DeletePaymentMethod(int id) {

            string query = "UPDATE payment_methods SET active = 0 WHERE payment_method_id = @payment_method_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@payment_method_id", SqlDbType.Int){Value  = id},
            };

            accessDB.ExecuteCommand(query, parameters);

        }
    }
}
