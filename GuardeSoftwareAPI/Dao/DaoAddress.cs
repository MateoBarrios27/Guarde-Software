using System;
using System.Data;
using Microsoft.Data.SqlClient;



namespace GuardeSoftwareAPI.Dao
{
    public class DaoAddress
    {
        private readonly AccessDB accessDB;

        public DaoAddress(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetAddress()
        {
            string query = "SELECT address_id, client_id, street, city, province FROM addresses";

            return accessDB.GetTable("addresses", query);
        }

        public DataTable GetAddressByClientId(int cliendId) {

            string query = "SELECT address_id, client_id, street, city, province FROM addresses WHERE client_id = @client_id ";

            SqlParameter[] parameters = new SqlParameter[] {

               new SqlParameter("@client_id", SqlDbType.Int ) {Value =  cliendId},
            };

            return accessDB.GetTable("addresses",query, parameters);
        }
    }
}
