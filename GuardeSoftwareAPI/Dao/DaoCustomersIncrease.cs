using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoClientsIncrease
	{
        private readonly AccessDB accessDB;

        public DaoClientsIncrease(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetClientsIncrease()
        {
            string consult = "SELECT client_id, policy_id, start_date, end_date, FROM clients_increase_policies";

            return accessDB.GetTable("clients_increase_policies", consult);
        }
    }
}
