using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoIncreaseRegimens
	{
        private readonly AccessDB accessDB;

        public DaoIncreasePolicies(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetIncreaseRegimens()
        {
            string consult = "SELECT regimen_id, frequency, percentage FROM increase_regimens";

            return accessDB.GetTable("increase_regimens",consult);
        }

    }
}
