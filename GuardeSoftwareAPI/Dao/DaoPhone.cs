using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
	public class DaoPhone
	{
        private readonly AccessDB accessDB;

        public DaoPhone(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetPhones()
        {
            string query = "SELECT phone_id, client_id, number, type, whatsapp FROM phones"
;
            return accessDB.GetTable("phones", query);
        }

        public DataTable GetPhonesByClientId(int clientId) {

            string query = "SELECT phone_id, client_id, number, type, whatsapp FROM phones WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return accessDB.GetTable("phones",query, parameters);
        }
    }
}