using System;
using System.Data;
using Microsoft.Data.SqlClient;

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
            string query = "SELECT activity_log_id, user_id, log_date, action, table_name, record_id, old_value, new_value FROM activity_log";

            return accessDB.GetTable("activity_log", query);
        }

        public DataTable GetActivityLogsByUserId(int userId) {

            string query = "SELECT activity_log_id, user_id, log_date, action, table_name, record_id, old_value, new_value FROM activity_log WHERE user_id = @user_id";

            SqlParameter[] parameters = new SqlParameter[] { 
                
                new SqlParameter("@user_id", SqlDbType.Int){Value  = userId},
            };

            return accessDB.GetTable("activity_log",query, parameters);
        }
    }
}
