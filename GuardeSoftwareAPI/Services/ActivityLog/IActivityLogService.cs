using GuardeSoftwareAPI.Dtos.ActivityLog;
using GuardeSoftwareAPI.Dtos.Common;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.activityLog
{
    public interface IActivityLogService
    {
        Task<List<ActivityLog>> GetActivityLogList();

        Task<PaginatedResultDto<ActivityLog>> GetActivityLogPageAsync(ActivityLogFilterDto filter);

        Task<List<ActivityLogUserDto>> GetActivityLogUsersAsync();

        Task<int?> GetCurrentUserIdAsync();

        Task<bool> IsCurrentUserAdminAsync();

        Task<List<ActivityLog>> GetActivityLoglistByUserId(int id);

        Task<bool> CreateActivityLog(ActivityLog activitylog);

        Task<bool> TryCreateActivityLogAsync(ActivityLog activityLog);

        Task<bool> DeleteActivityLog(int id);

        Task<bool> CreateActivityLogTransactionAsync(ActivityLog activityLog, SqlConnection connection, SqlTransaction transaction);
    }
}
