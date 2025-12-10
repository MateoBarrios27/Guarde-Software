using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao {

    public class DaoWarehouse
    {
        private readonly AccessDB accessDB;

        public DaoWarehouse(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetWarehouses()
        {
            string query = "SELECT warehouse_id, name, address FROM warehouses WHERE active = 1";
            return await accessDB.GetTableAsync("warehouses", query);
        }

        public async Task<DataTable> GetWarehouseById(int id)
        {

            string query = "SELECT warehouse_id, name, address FROM warehouses WHERE warehouse_id = @warehouse_id AND active = 1";

            SqlParameter[] parameters =
            [
                new("@warehouse_id", SqlDbType.Int ) { Value = id}
            ];
            return await accessDB.GetTableAsync("warehouses", query, parameters);
        }

        public async Task<int> CreateWarehouseAsync(string name, string? address)
        {
            string query = "INSERT INTO warehouses (name, address) OUTPUT INSERTED.warehouse_id VALUES (@Name, @Address)";
            SqlParameter[] parameters = [
                new("@Name", SqlDbType.VarChar, 100) { Value = name },
                new("@Address", SqlDbType.VarChar, 255) { Value = (object?)address ?? DBNull.Value }
            ];
            
            object result = await accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToInt32(result);
        }

        public async Task<bool> UpdateWarehouseAsync(int id, string name, string? address)
        {
            string query = "UPDATE warehouses SET name = @Name, address = @Address WHERE warehouse_id = @Id";
            SqlParameter[] parameters = [
                new("@Name", SqlDbType.VarChar, 100) { Value = name },
                new("@Address", SqlDbType.VarChar, 255) { Value = (object?)address ?? DBNull.Value },
                new("@Id", SqlDbType.Int) { Value = id }
            ];
            
            int rows = await accessDB.ExecuteCommandAsync(query, parameters);
            return rows > 0;
        }
        
        public async Task<bool> DeleteWarehouseAsync(int id)
        {
            string query = "UPDATE warehouses SET active = 0 WHERE warehouse_id = @Id";
            SqlParameter[] parameters = [new SqlParameter("@Id", SqlDbType.Int) { Value = id }];
            
            int rows = await accessDB.ExecuteCommandAsync(query, parameters);
            return rows > 0;
        }

        public async Task<bool> HasActiveLockersAsync(int warehouseId)
        {
            string query = "SELECT COUNT(1) FROM lockers WHERE warehouse_id = @Id AND status != 'Eliminado'"; 
            SqlParameter[] parameters = [new SqlParameter("@Id", SqlDbType.Int) { Value = warehouseId }];
            
            object result = await accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToInt32(result) > 0;
        }
    }
}