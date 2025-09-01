using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao { 

	public class DaoWarehouse
	{
		private readonly AccessDB accessDB;

		public DaoWarehouse(AccessDB _accessDB)
		{
			accessDB = _accessDB;
		}

		public DataTable GetWarehouses()
		{
			string query = "SELECT warehouse_id, name, address FROM warehouses WHERE active = 1";
			return accessDB.GetTable("warehouses", query);
		}

        public DataTable GetWarehouseById(int id) {

            string query = "SELECT warehouse_id, name, address FROM warehouses WHERE warehouse_id = @warehouse_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@warehouse_id", SqlDbType.Int ) { Value = id}
            };
            return accessDB.GetTable("warehouses", query, parameters);
        }

		public bool DeleteWarehouse(int id) {

            string query = "UPDATE warehouses SET active = 0 WHERE warehouse_id = @warehouse_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@warehouse_id", SqlDbType.Int ) { Value = id},
            };

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }
    }

}