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

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@warehouse_id", SqlDbType.Int ) { Value = id}
            };
            return await accessDB.GetTableAsync("warehouses", query, parameters);
        }

        public async Task<Warehouse> CreateWarehouse(Warehouse warehouse)
        {
            string query = "INSERT INTO warehouses (name, address) VALUES (@name, @address); SELECT SCOPE_IDENTITY();";
            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@name", SqlDbType.NVarChar, 100){Value  = warehouse.Name},
                new SqlParameter("@address", SqlDbType.NVarChar, 200){Value  = warehouse.Address},
            };
            
             object newId = await accessDB.ExecuteScalarAsync(query, parameters);

            if (newId != null && newId != DBNull.Value)
            {
                //Assign the newly generated ID to the warehouse object
                warehouse.Id = Convert.ToInt32(newId);
            }

            return warehouse;
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