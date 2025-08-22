using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{

	public class DaoClients
	{
        private readonly AccessDB accessDB;

        public DaoClients(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetClients() {

            string consult = "SELECT client_id, payment_identifier,first_name,last_name,registration_date,document_number,tax_id,preferred_payment_method_id,tax_condition, notes FROM clients WHERE active=1";

            return accessDB.GetTable("clients",consult);
        }

        public DataTable GetClientById(int id)
        {
            string consult = "SELECT client_id, payment_identifier,first_name,last_name,registration_date,document_number,tax_id,preferred_payment_method_id,tax_condition, notes FROM clients WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id",SqlDbType.Int) {Value = id},
            };

            return accessDB.GetTable("clients",consult,parameters);
        }

        public void DeleteClientById(int id) {

            string consult = "UPDATE clients SET active = 0 WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int){Value = id},
            };
            
            accessDB.ExecuteCommand(consult,parameters);
        
        }
    }
}

