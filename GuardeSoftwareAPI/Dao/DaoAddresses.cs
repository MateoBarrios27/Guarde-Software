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
            string consult = "SELECT addresses_id, customer_id, address, city, state FROM addresses";

            return accessDB.GetTable("addresses", consult);
        }
    }
}
