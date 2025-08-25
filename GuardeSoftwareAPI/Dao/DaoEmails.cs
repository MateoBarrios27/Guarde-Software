using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoEmails
    {
        private readonly AccessDB accessDB;

        public DaoEmails(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetEmails()
        {
            string query = "SELECT email_id, client_id, address, type FROM emails";

            return accessDB.GetTable("emails", query);
        }

        public DataTable GetEmailByClientId(string clientId) {

            string query = "SELECT email_id, client_id, address, type FROM emails WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value =  clientId},
            };
            
            return accessDB.GetTable("emails", query, parameters);
        }
    }
}
