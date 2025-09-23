using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoClientIncreaseRegimen
	{
        private readonly AccessDB accessDB;

        public DaoClientIncreaseRegimen(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetClientIncreaseRegimens()
        {
            string query = "SELECT client_id, regimen_id, start_date, end_date FROM clients_x_increase_regimens";

            return await accessDB.GetTableAsync("clients_x_increase_regimens", query);
        }

        public async Task<DataTable> GetClientIncreaseRegimensByClientId(int clientId) {

            string query = "SELECT client_id, regimen_id, start_date, end_date FROM clients_x_increase_regimens WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

               new SqlParameter("@client_id", SqlDbType.Int ) {Value =  clientId},
            };

            return await accessDB.GetTableAsync("clients_x_increase_regimens", query, parameters);
        }

        public async Task<bool> CreateClientIncreaseRegimen(ClientIncreaseRegimen clientIncrease)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int){Value = clientIncrease.ClientId},
                new SqlParameter("@start_date", SqlDbType.DateTime){Value = clientIncrease.StartDate},
                new SqlParameter("@end_date", SqlDbType.DateTime){Value = clientIncrease.EndDate == null ? DBNull.Value : clientIncrease.EndDate},
            };

            string query = "INSERT INTO clients_x_increase_regimens(client_id, start_date, end_date)VALUES(@client_id, @start_date, @end_date)";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
    }
}
