using System;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.activityLog {

	public interface IActivityLogService
	{
		Task<List<ActivityLog>> GetActivityLogList();

		Task<List<ActivityLog>> GetActivityLoglistByUserId(int id);

		Task<bool> CreateActivityLog(ActivityLog activitylog);

		Task<bool> DeleteActivityLog(int id);

		Task<bool> CreateActivityLogTransactionAsync(ActivityLog activityLog, SqlConnection connection, SqlTransaction transaction);

    }
}