using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoPayment
	{
        private readonly AccessDB accessDB;

        public DaoPayment(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }
        
        public DataTable GetPayments()
        {
            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments";

            return accessDB.GetTable("payments", query);
        }

        public DataTable GetPaymentsById(int id) {

            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments WHERE payment_id = @payment_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@payment_id", SqlDbType.Int){Value  = id},
            };

            return accessDB.GetTable("payments",query, parameters);
        }

        public DataTable GetPaymentsByClientId(int clientId) {

            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments WHERE client_id = @client_id";


            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return accessDB.GetTable("payments", query, parameters);

        }
    }
}
