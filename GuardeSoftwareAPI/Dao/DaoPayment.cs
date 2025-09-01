using System;
using System.Data;
using GuardeSoftwareAPI.Entities;
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

        public DataTable GetPaymentById(int id)
        {

            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments WHERE payment_id = @payment_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@payment_id", SqlDbType.Int){Value  = id},
            };

            return accessDB.GetTable("payments", query, parameters);
        }

        public bool CreatePayment(Payment payment)
        {
            string query = "INSERT INTO payments (client_id, payment_method_id, payment_date, amount) VALUES (@client_id, @payment_method_id, @payment_date, @amount)";

            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@client_id", SqlDbType.Int){Value  = payment.ClientId},
                new SqlParameter("@payment_method_id", SqlDbType.Int){Value  = payment.PaymentMethodId},
                new SqlParameter("@payment_date", SqlDbType.DateTime){Value  = payment.PaymentDate},
                new SqlParameter("@amount", SqlDbType.Decimal){Value  = payment.Amount},
            };

            return  accessDB.ExecuteCommand(query, parameters) > 0;
        }

        public DataTable GetPaymentsByClientId(int clientId)
        {

            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments WHERE client_id = @client_id";


            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return accessDB.GetTable("payments", query, parameters);

        }
        
    }
}
