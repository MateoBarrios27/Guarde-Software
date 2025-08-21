using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoActivityLog
	{
        private readonly AccessDB accessDB;

        public DaoActivityLog(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetActivityLog()
        {
            string consult = "SELECT activity_log_id, user_id, log_date, action_table_name, record_id, old_value, new_value FROM activity_log";

            return accessDB.GetTable("activity_log", consult);
        }
    }
}
