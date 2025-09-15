using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;

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

        public DataTable GetActivityLogsByUserId(int userId)
        {

            string query = "SELECT activity_log_id, user_id, log_date, action, table_name, record_id, old_value, new_value FROM activity_log WHERE user_id = @user_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@user_id", SqlDbType.Int){Value  = userId},
            };

            return accessDB.GetTable("activity_log", query, parameters);
        }

        public bool CreateActivityLog(ActivityLog activityLog)
        {

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@user_id", SqlDbType.Int){Value = activityLog.UserId},
                new SqlParameter("@log_date",SqlDbType.DateTime){Value = activityLog.LogDate },
                new SqlParameter("@action", SqlDbType.VarChar){Value = activityLog.Action },
                new SqlParameter("@table_name", SqlDbType.VarChar){Value = activityLog.TableName },
                new SqlParameter("@record_id", SqlDbType.Int){Value = activityLog.RecordId },
                new SqlParameter("@old_value", SqlDbType.NVarChar){Value = activityLog.OldValue },
                new SqlParameter("@new_value", SqlDbType.NVarChar){Value = activityLog.NewValue },
            };

            string query = "INSERT INTO activity_log (user_id, log_date, action, table_name, record_id, old_value, new_value)"
                + "VALUES(@user_id, @log_date, @action, @table_name, @record_id, @old_value, @new_value)";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }
        
        public bool DeleteActivityLog(int activityLogId)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@activity_log_id", SqlDbType.Int){Value = activityLogId},
            };

            string query = "DELETE FROM activity_log WHERE activity_log_id = @activity_log_id";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

        public async Task<bool> CreateActivityLogTransactionAsync(ActivityLog activityLog, SqlConnection connection, SqlTransaction transaction)
        {
            if(activityLog == null) throw new ArgumentNullException(nameof(activityLog));

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@user_id", SqlDbType.Int){Value = activityLog.UserId},
                new SqlParameter("@log_date",SqlDbType.DateTime){Value = activityLog.LogDate },
                new SqlParameter("@action", SqlDbType.VarChar){Value = activityLog.Action },
                new SqlParameter("@table_name", SqlDbType.VarChar){Value = activityLog.TableName },
                new SqlParameter("@record_id", SqlDbType.Int){Value = activityLog.RecordId },
                new SqlParameter("@old_value", SqlDbType.NVarChar)
                {
                 Value = (object?)activityLog.OldValue ?? DBNull.Value
                },
                new SqlParameter("@new_value", SqlDbType.NVarChar)
                {
                    Value = (object?)activityLog.NewValue ?? DBNull.Value
                },      
            };

            string query = "INSERT INTO activity_log (user_id, log_date, action, table_name, record_id, old_value, new_value)"
                + "VALUES(@user_id, @log_date, @action, @table_name, @record_id, @old_value, @new_value)";

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                int rows = await command.ExecuteNonQueryAsync();

                if (rows <= 0) return false;
            }
            return true;
        }

    }
}
