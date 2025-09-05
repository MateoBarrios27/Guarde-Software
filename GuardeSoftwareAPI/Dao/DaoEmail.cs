using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoEmail
    {
        private readonly AccessDB accessDB;

        public DaoEmail(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetEmails()
        {
            string query = "SELECT email_id, client_id, address, type FROM emails WHERE active = 1";

            return accessDB.GetTable("emails", query);
        }

        public DataTable GetEmailsByClientId(int clientId) {

            string query = "SELECT email_id, client_id, address, type FROM emails WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value =  clientId},
            };
            
            return accessDB.GetTable("emails", query, parameters);
        }

        public bool CreateEmail(Email email) {

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id",SqlDbType.Int){Value = email.ClientId},
                new SqlParameter("@address",SqlDbType.VarChar,150){Value = email.Address},
                new SqlParameter("@type",SqlDbType.VarChar,50){Value = (object?)email.Type ?? DBNull.Value},
            };

            string query = "INSERT INTO emails(client_id, address, type)VALUES(@client_id, @address, @type)";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }
    }
}
