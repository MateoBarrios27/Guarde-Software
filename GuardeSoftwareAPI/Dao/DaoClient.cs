using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{

	public class DaoClient
	{
        private readonly AccessDB accessDB;

        public DaoClient(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetClients() {

            string query = "SELECT client_id, payment_identifier,first_name,last_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes FROM clients WHERE active=1";

            return accessDB.GetTable("clients", query);
        }

        public DataTable GetClientById(int id)
        {
            string query = "SELECT client_id, payment_identifier,first_name,last_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes FROM clients WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id",SqlDbType.Int) {Value = id},
            };

            return accessDB.GetTable("clients", query, parameters);
        }

        public bool DeleteClientById(int id) {

            string query = "UPDATE clients SET active = 0 WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int){Value = id},
            };
            
            return accessDB.ExecuteCommand(query, parameters) > 0;
        
        }
    }
}

