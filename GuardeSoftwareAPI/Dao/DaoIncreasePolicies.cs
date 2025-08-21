using System;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoIncreasePolicies
	{
        private readonly AccessDB accessDB;

        public DaoIncreasePolicies(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public GetIncreasePolicies()
        {
            string consult = "SELECT policy_id, frequency, percentage FROM increase_policies";
        }

    }
}
