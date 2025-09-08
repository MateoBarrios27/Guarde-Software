using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.activityLog {

	public interface IActivityLogService
	{
		List<ActivityLog> GetActivityLogList();

		List<ActivityLog> GetActivityLoglistByUserId(int id);

		public bool CreateActivityLog(ActivityLog activitylog);
		public bool DeleteActivityLog(int id);
	}
}