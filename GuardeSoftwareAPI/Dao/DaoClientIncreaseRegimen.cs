using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoClientIncreaseRegimen
	{
        private readonly AccessDB accessDB;

        public DaoClientIncreaseRegimen(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetClientIncreaseRegimens()
        {
            string query = "SELECT client_id, regimen_id, start_date, end_date FROM clients_x_increase_regimens";

            return accessDB.GetTable("clients_x_increase_regimens", query);
        }

        public DataTable GetClientIncreaseRegimensByClientId(string clientId) {

            string query = "SELECT client_id, regimen_id, start_date, end_date FROM clients_x_increase_regimens WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

               new SqlParameter("@client_id", SqlDbType.Int ) {Value =  clientId},
            };

            return accessDB.GetTable("clients_x_increase_regimens", query, parameters);
        }
    }
}
