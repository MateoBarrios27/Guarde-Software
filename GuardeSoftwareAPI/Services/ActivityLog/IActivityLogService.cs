using System;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.activityLog {

	public interface IActivityLogService
	{
		List<ActivityLog> GetActivityLogList();

		List<ActivityLog> GetActivityLoglistByUserId(int id);

		public bool CreateActivityLog(ActivityLog activitylog);

		public bool DeleteActivityLog(int id);

		Task<bool> CreateActivityLogTransactionAsync(ActivityLog activityLog, SqlConnection connection, SqlTransaction transaction);

    }
}