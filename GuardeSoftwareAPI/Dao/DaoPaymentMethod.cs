using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
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

        public async Task<DataTable> GetPaymentMethods()
        {
            string query = "SELECT payment_method_id, name, commission FROM payment_methods WHERE active = 1";

            return await accessDB.GetTableAsync("payment_methods", query);
        }

        public async Task<DataTable> GetPaymentMethodById(int id) { 
        
            string query = "SELECT payment_method_id, name, commission FROM payment_methods WHERE active = 1 AND payment_method_id = @payment_method_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@payment_method_id", SqlDbType.Int){Value  = id},
            };
            return await accessDB.GetTableAsync("payment_methods", query, parameters);
        }

        public async Task<bool> CreatePaymentMethod(PaymentMethod paymentMethod) {

            string query = "INSERT INTO payment_methods (name,commission, active) VALUES (@name, @commission, 1)";
            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@name", SqlDbType.NVarChar, 100){Value  = paymentMethod.Name},
                new SqlParameter("@commission", SqlDbType.Decimal){Value  = paymentMethod.Commission},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<bool> CheckIfPaymentMethodExists(string name)
        {
            string query = "SELECT COUNT(*) FROM payment_methods WHERE name = @name AND active = 1";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@name", SqlDbType.NVarChar, 100) { Value = name }
            };

            object result = await accessDB.ExecuteScalarAsync(query, parameters);
            int count = (result != null && int.TryParse(result.ToString(), out int tempCount)) ? tempCount : 0;

            return count > 0;
        }
        
        public async Task<bool> DeletePaymentMethod(int id)
        {

            string query = "UPDATE payment_methods SET active = 0 WHERE payment_method_id = @payment_method_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@payment_method_id", SqlDbType.Int){Value  = id},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
    }
}
