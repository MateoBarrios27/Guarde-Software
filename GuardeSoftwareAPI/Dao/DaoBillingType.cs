using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoBillingType
    {
        private readonly AccessDB _accessDB;

        public DaoBillingType(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        public async Task<DataTable> GetBillingTypesAsync()
        {
            string query = "SELECT billing_type_id, name, active FROM billing_types WHERE active = 1 ORDER BY name";
            return await _accessDB.GetTableAsync("billing_types", query);
        }

        public async Task<int> CreateBillingTypeAsync(string name)
        {
            string query = "INSERT INTO billing_types (name) OUTPUT INSERTED.billing_type_id VALUES (@Name)";
            SqlParameter[] parameters = {
                new SqlParameter("@Name", SqlDbType.VarChar, 100) { Value = name }
            };
            
            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            if (result == null || result == DBNull.Value)
            {
                throw new InvalidOperationException("No se pudo crear el tipo de factura.");
            }
            return Convert.ToInt32(result);
        }

        public async Task<bool> UpdateBillingTypeAsync(int id, string name)
        {
            string query = "UPDATE billing_types SET name = @Name WHERE billing_type_id = @Id";
            SqlParameter[] parameters = {
                new SqlParameter("@Name", SqlDbType.VarChar, 100) { Value = name },
                new SqlParameter("@Id", SqlDbType.Int) { Value = id }
            };
            
            int rowsAffected = await _accessDB.ExecuteCommandAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<bool> IsBillingTypeInUseAsync(int id)
        {
            string query = "SELECT TOP 1 1 FROM clients WHERE billing_type_id = @Id";
            SqlParameter[] parameters = {
                new SqlParameter("@Id", SqlDbType.Int) { Value = id }
            };

            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return (result != null && result != DBNull.Value);
        }

        public async Task<bool> DeleteBillingTypeAsync(int id)
        {
            string query = "DELETE FROM billing_types WHERE billing_type_id = @Id";
            SqlParameter[] parameters = {
                new SqlParameter("@Id", SqlDbType.Int) { Value = id }
            };
            
            int rowsAffected = await _accessDB.ExecuteCommandAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<DataRow?> GetBillingTypeByIdAsync(int id)
        {
             string query = "SELECT billing_type_id, name, active FROM billing_types WHERE billing_type_id = @Id";
             SqlParameter[] parameters = {
                new SqlParameter("@Id", SqlDbType.Int) { Value = id }
            };
            DataTable table = await _accessDB.GetTableAsync("billing_type", query, parameters);
            return table.Rows.Count > 0 ? table.Rows[0] : null;
        }
    }
}