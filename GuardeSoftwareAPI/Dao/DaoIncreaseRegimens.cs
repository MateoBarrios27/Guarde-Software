using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoIncreaseRegimens
	{
        private readonly AccessDB accessDB;

        public DaoIncreaseRegimens(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetIncreaseRegimens()
        {
            string query = "SELECT regimen_id, frequency, percentage FROM increase_regimens";

            return accessDB.GetTable("increase_regimens",query);
        }

    }
}
