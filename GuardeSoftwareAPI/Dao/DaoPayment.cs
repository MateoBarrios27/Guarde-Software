using System;
using System.Data;
using System.Threading.Tasks;
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

        public async Task<DataTable> GetPayments()
        {
            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments";

            return await accessDB.GetTableAsync("payments", query);
        }

        public async Task<DataTable> GetPaymentById(int id)
        {

            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments WHERE payment_id = @payment_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@payment_id", SqlDbType.Int){Value  = id},
            };

            return await accessDB.GetTableAsync("payments", query, parameters);
        }

        public async Task<bool> CreatePayment(Payment payment)
        {
            string query = "INSERT INTO payments (client_id, payment_method_id, payment_date, amount) VALUES (@client_id, @payment_method_id, @payment_date, @amount)";

            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@client_id", SqlDbType.Int){Value  = payment.ClientId},
                new SqlParameter("@payment_method_id", SqlDbType.Int){Value  = payment.PaymentMethodId},
                new SqlParameter("@payment_date", SqlDbType.DateTime){Value  = payment.PaymentDate},
                new SqlParameter("@amount", SqlDbType.Decimal){Value  = payment.Amount},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<int> CreatePaymentTransactionAsync(Payment payment, SqlConnection connection, SqlTransaction transaction)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));

            string query = @"
                INSERT INTO payments (client_id, payment_method_id, payment_date, amount)
                OUTPUT INSERTED.payment_id
                VALUES (@client_id, @payment_method_id, @payment_date, @amount);";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new("@client_id", SqlDbType.Int) { Value = payment.ClientId },
                new("@payment_method_id", SqlDbType.Int) { Value = payment.PaymentMethodId },
                new("@payment_date", SqlDbType.DateTime) { Value = payment.PaymentDate },
                new("@amount", SqlDbType.Decimal) { Value = payment.Amount }
            };

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);

            object result = await command.ExecuteScalarAsync() ?? DBNull.Value;

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("No se pudo obtener el ID del nuevo Payment.");

            return Convert.ToInt32(result);
        }

        public async Task<DataTable> GetPaymentsByClientId(int clientId)
        {

            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments WHERE client_id = @client_id";


            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return await accessDB.GetTableAsync("payments", query, parameters);

        }
        
    }
}
