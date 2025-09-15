using System;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using System.Data;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;


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

        public bool CreateActivityLog(ActivityLog activityLog)
        {

            if (activityLog == null)
                throw new ArgumentNullException(nameof(activityLog));

            if (activityLog.UserId <= 0)
                throw new ArgumentException("Invalid UserId.");

            if (string.IsNullOrWhiteSpace(activityLog.Action))
                throw new ArgumentException("Action is required.");

            if (string.IsNullOrWhiteSpace(activityLog.TableName))
                throw new ArgumentException("TableName is required.");

            if (activityLog.RecordId <= 0)
                throw new ArgumentException("Invalid RecordId.");

            activityLog.OldValue = string.IsNullOrWhiteSpace(activityLog.OldValue) ? null : activityLog.OldValue;
            activityLog.NewValue = string.IsNullOrWhiteSpace(activityLog.NewValue) ? null : activityLog.NewValue;

            if (_daoActivityLog.CreateActivityLog(activityLog)) return true;
            else return false;
        }

        public async Task<bool> CreateActivityLogTransactionAsync(ActivityLog activityLog, SqlConnection connection, SqlTransaction transaction)
        {

            if (activityLog == null)
                throw new ArgumentNullException(nameof(activityLog));

            if (activityLog.UserId <= 0)
                throw new ArgumentException("Invalid UserId.");

            if (string.IsNullOrWhiteSpace(activityLog.Action))
                throw new ArgumentException("Action is required.");

            if (string.IsNullOrWhiteSpace(activityLog.TableName))
                throw new ArgumentException("TableName is required.");

            if (activityLog.RecordId <= 0)
                throw new ArgumentException("Invalid RecordId.");

            activityLog.OldValue = string.IsNullOrWhiteSpace(activityLog.OldValue) ? null : activityLog.OldValue;
            activityLog.NewValue = string.IsNullOrWhiteSpace(activityLog.NewValue) ? null : activityLog.NewValue;

            return await _daoActivityLog.CreateActivityLogTransactionAsync(activityLog, connection, transaction);
        }

        public bool DeleteActivityLog(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid ActivityLog Id.");

            if (_daoActivityLog.DeleteActivityLog(id)) return true;
            else return false;
        }
    }
}