using System;
using System.Data;



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
    }
}
