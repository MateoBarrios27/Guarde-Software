using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoIncreasePolicies
	{
        private readonly AccessDB accessDB;

        public DaoIncreasePolicies(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetIncreasePolicies()
        {
            string consult = "SELECT policy_id, frequency, percentage FROM increase_policies";

            return accessDB.GetTable("increase_policies",consult);
        }

    }
}
