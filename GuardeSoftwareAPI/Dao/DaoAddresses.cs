using System;
using System.Data;
using Microsoft.Data.SqlClient;



namespace GuardeSoftwareAPI.Dao
{
    public class DaoAddresses
    {
        private readonly AccessDB accessDB;

        public DaoAddresses(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetAddresses()
        {
            string query = "SELECT address_id, client_id, street, city, province FROM addresses";

            return accessDB.GetTable("addresses", query);
        }

        public DataTable GetAddressByCliendId(string cliendId) {

            string query = "SELECT address_id, client_id, street, city, province FROM addresses WHERE client_id = @client_id ";

            SqlParameter[] parameters = new SqlParameter[] {

               new SqlParameter("@client_id", SqlDbType.Int ) {Value =  cliendId},
            };

            return accessDB.GetTable("addresses",query, parameters);
        }
    }
}
