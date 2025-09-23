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

            string query = "SELECT warehouse_id, name, address FROM warehouses WHERE warehouse_id = @warehouse_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@warehouse_id", SqlDbType.Int ) { Value = id}
            };
            return await accessDB.GetTableAsync("warehouses", query, parameters);
        }

        public async Task<bool> CreateWarehouse(Warehouse warehouse)
        {
            string query = "INSERT INTO warehouses (name, address) VALUES (@name, @address)";
            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@name", SqlDbType.NVarChar, 100){Value  = warehouse.Name},
                new SqlParameter("@address", SqlDbType.NVarChar, 200){Value  = warehouse.Address},
            };
            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
        
        public async Task<bool> DeleteWarehouse(int id)
        {

            string query = "UPDATE warehouses SET active = 0 WHERE warehouse_id = @warehouse_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@warehouse_id", SqlDbType.Int ) { Value = id},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }


    }
}