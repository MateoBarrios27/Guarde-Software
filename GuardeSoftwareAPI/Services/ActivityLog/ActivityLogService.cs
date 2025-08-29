using System;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using System.Data;
using System.Collections.Generic;

namespace GuardeSoftwareAPI.Services.activityLog
{

	public class ActivityLogService : IActivityLogService
    {
		private readonly DaoActivityLog _daoActivityLog;

		public ActivityLogService(AccessDB accessDB)
		{
			_daoActivityLog = new DaoActivityLog(accessDB);
		}
		
		public List<ActivityLog> GetActivityLogList()
		{
			DataTable activityTable = _daoActivityLog.GetActivityLog();
			List<ActivityLog> activityLog = new List<ActivityLog>();

			foreach (DataRow row in activityTable.Rows)
			{
				int activityId = (int)row["activity_log_id"];

				ActivityLog activity = new ActivityLog
				{
					Id = activityId,

					UserId = row["user_id"] != DBNull.Value
					? (int)row["user_id"] : 0,

					LogDate = (DateTime)row["log_date"],

					Action = row["action"]?.ToString() ?? string.Empty,

					TableName = row["table_name"]?.ToString() ?? string.Empty,

					RecordId = row["record_id"] != DBNull.Value
					? (int)row["record_id"] : 0,

					OldValue = row["old_value"]?.ToString() ?? string.Empty,

					NewValue = row["new_value"]?.ToString() ?? string.Empty,
				};
				activityLog.Add(activity);
			}
			return activityLog;
		}

        public List<ActivityLog> GetActivityLoglistByUserId(int id)
        {
            DataTable activityTable = _daoActivityLog.GetActivityLogsByUserId(id);
            List<ActivityLog> activityLog = new List<ActivityLog>();

            foreach (DataRow row in activityTable.Rows)
            {
                int activityId = (int)row["activity_log_id"];

                ActivityLog activity = new ActivityLog
                {
                    Id = activityId,

                    UserId = row["user_id"] != DBNull.Value
                    ? (int)row["user_id"] : 0,

                    LogDate = (DateTime)row["log_date"],

                    Action = row["action"]?.ToString() ?? string.Empty,

                    TableName = row["table_name"]?.ToString() ?? string.Empty,

                    RecordId = row["record_id"] != DBNull.Value
                    ? (int)row["record_id"] : 0,

                    OldValue = row["old_value"]?.ToString() ?? string.Empty,

                    NewValue = row["new_value"]?.ToString() ?? string.Empty,
                };
                activityLog.Add(activity);
            }
            return activityLog;
        }
    }
}