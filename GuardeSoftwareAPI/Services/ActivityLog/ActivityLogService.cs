using System.Data;
using System.Security.Claims;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.ActivityLog;
using GuardeSoftwareAPI.Dtos.Common;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.activityLog
{
    public class ActivityLogService : IActivityLogService
    {
        private static readonly HashSet<string> AllowedAreas = new(StringComparer.OrdinalIgnoreCase)
        {
            "clients",
            "payments",
            "lockers",
            "payment_methods",
            "communications",
            "warehouses",
            "locker_types",
            "rental_amount_history",
            "users",
            "auth"
        };

        private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "CREATE",
            "UPDATE",
            "DELETE",
            "DEACTIVATE",
            "REACTIVATE",
            "LOGIN"
        };

        private readonly DaoActivityLog _daoActivityLog;
        private readonly DaoUser _daoUser;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ActivityLogService> _logger;

        public ActivityLogService(
            AccessDB accessDB,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ActivityLogService> logger)
        {
            _daoActivityLog = new DaoActivityLog(accessDB);
            _daoUser = new DaoUser(accessDB);
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<List<ActivityLog>> GetActivityLogList()
        {
            DataTable activityTable = await _daoActivityLog.GetActivityLog();
            return MapActivityLogs(activityTable);
        }

        public async Task<PaginatedResultDto<ActivityLog>> GetActivityLogPageAsync(ActivityLogFilterDto filter)
        {
            filter ??= new ActivityLogFilterDto();
            filter.PageNumber = Math.Max(1, filter.PageNumber);
            filter.PageSize = Math.Clamp(filter.PageSize, 10, 100);

            filter.Area = NormalizeFilterValue(filter.Area, AllowedAreas);
            filter.Action = NormalizeFilterValue(filter.Action, AllowedActions)?.ToUpperInvariant();

            if (filter.UserId is <= 0)
                filter.UserId = null;

            var result = await _daoActivityLog.GetActivityLogPageAsync(filter);
            return new PaginatedResultDto<ActivityLog>
            {
                Items = MapActivityLogs(result.Items),
                TotalCount = result.TotalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<List<ActivityLogUserDto>> GetActivityLogUsersAsync()
        {
            DataTable usersTable = await _daoActivityLog.GetActivityLogUsersAsync();
            var users = new List<ActivityLogUserDto>();

            foreach (DataRow row in usersTable.Rows)
            {
                users.Add(new ActivityLogUserDto
                {
                    Id = Convert.ToInt32(row["user_id"]),
                    UserName = row["username"]?.ToString() ?? string.Empty,
                    DisplayName = row["display_name"]?.ToString() ?? row["username"]?.ToString() ?? string.Empty
                });
            }

            return users;
        }

        public async Task<int?> GetCurrentUserIdAsync()
        {
            ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            string? businessUserId = principal.FindFirst("businessUserId")?.Value;
            if (int.TryParse(businessUserId, out int parsedBusinessUserId) && parsedBusinessUserId > 0)
                return parsedBusinessUserId;

            string? identityUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(identityUserId))
                return null;

            DataTable table = await _daoUser.GetUserByIdentityUserId(identityUserId);
            if (table.Rows.Count == 0)
                return null;

            return Convert.ToInt32(table.Rows[0]["user_id"]);
        }

        public async Task<bool> IsCurrentUserAdminAsync()
        {
            ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User;
            string? businessUserTypeId = principal?.FindFirst("businessUserTypeId")?.Value;
            if (int.TryParse(businessUserTypeId, out int parsedTypeId))
                return parsedTypeId == 1;

            int? currentUserId = await GetCurrentUserIdAsync();
            if (!currentUserId.HasValue)
                return false;

            DataTable table = await _daoUser.GetUserById(currentUserId.Value);
            return table.Rows.Count > 0 && Convert.ToInt32(table.Rows[0]["user_type_id"]) == 1;
        }

        public async Task<List<ActivityLog>> GetActivityLoglistByUserId(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid user ID.");

            DataTable activityTable = await _daoActivityLog.GetActivityLogsByUserId(id);
            return MapActivityLogs(activityTable);
        }

        public async Task<bool> CreateActivityLog(ActivityLog activityLog)
        {
            await PrepareActivityLogAsync(activityLog);
            return await _daoActivityLog.CreateActivityLog(activityLog);
        }

        public async Task<bool> TryCreateActivityLogAsync(ActivityLog activityLog)
        {
            try
            {
                return await CreateActivityLog(activityLog);
            }
            catch (Exception ex)
            {
                // La operación principal ya fue confirmada en los CRUD simples;
                // no se revierte ni se convierte en un falso error de negocio por
                // una falla secundaria del registro de auditoría.
                _logger.LogError(ex, "No se pudo guardar la actividad {Action} {TableName} {RecordId}", activityLog.Action, activityLog.TableName, activityLog.RecordId);
                return false;
            }
        }

        public async Task<bool> CreateActivityLogTransactionAsync(ActivityLog activityLog, SqlConnection connection, SqlTransaction transaction)
        {
            await PrepareActivityLogAsync(activityLog);
            return await _daoActivityLog.CreateActivityLogTransactionAsync(activityLog, connection, transaction);
        }

        public async Task<bool> DeleteActivityLog(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid ActivityLog Id.");

            return await _daoActivityLog.DeleteActivityLog(id);
        }

        private async Task PrepareActivityLogAsync(ActivityLog activityLog)
        {
            if (activityLog == null)
                throw new ArgumentNullException(nameof(activityLog));

            int? currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId.HasValue)
                activityLog.UserId = currentUserId.Value;

            if (activityLog.UserId <= 0)
                throw new ArgumentException("Invalid UserId.");

            if (string.IsNullOrWhiteSpace(activityLog.Action))
                throw new ArgumentException("Action is required.");

            if (string.IsNullOrWhiteSpace(activityLog.TableName))
                throw new ArgumentException("TableName is required.");

            if (activityLog.RecordId <= 0)
                throw new ArgumentException("Invalid RecordId.");

            activityLog.LogDate = activityLog.LogDate == default
                ? TimeHelper.GetArgentinaTime()
                : activityLog.LogDate;
            activityLog.Action = activityLog.Action.Trim().ToUpperInvariant();
            activityLog.TableName = activityLog.TableName.Trim().ToLowerInvariant();
            activityLog.OldValue = string.IsNullOrWhiteSpace(activityLog.OldValue) ? null : activityLog.OldValue;
            activityLog.NewValue = string.IsNullOrWhiteSpace(activityLog.NewValue) ? null : activityLog.NewValue;
        }

        private static List<ActivityLog> MapActivityLogs(DataTable activityTable)
        {
            var activityLogs = new List<ActivityLog>();
            foreach (DataRow row in activityTable.Rows)
            {
                activityLogs.Add(new ActivityLog
                {
                    Id = Convert.ToInt32(row["activity_log_id"]),
                    UserId = row["user_id"] != DBNull.Value ? Convert.ToInt32(row["user_id"]) : 0,
                    LogDate = row["log_date"] != DBNull.Value ? Convert.ToDateTime(row["log_date"]) : DateTime.MinValue,
                    Action = row["action"]?.ToString() ?? string.Empty,
                    TableName = row["table_name"]?.ToString() ?? string.Empty,
                    RecordId = row["record_id"] != DBNull.Value ? Convert.ToInt32(row["record_id"]) : 0,
                    OldValue = row["old_value"] == DBNull.Value ? null : row["old_value"]?.ToString(),
                    NewValue = row["new_value"] == DBNull.Value ? null : row["new_value"]?.ToString(),
                    UserName = row.Table.Columns.Contains("user_name") && row["user_name"] != DBNull.Value
                        ? row["user_name"]?.ToString() ?? string.Empty
                        : string.Empty,
                    UserDisplayName = row.Table.Columns.Contains("user_display_name") && row["user_display_name"] != DBNull.Value
                        ? row["user_display_name"]?.ToString() ?? string.Empty
                        : string.Empty
                });
            }

            return activityLogs;
        }

        private static string? NormalizeFilterValue(string? value, HashSet<string> allowedValues)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalizedValue = value.Trim();
            return allowedValues.Contains(normalizedValue) ? normalizedValue.ToLowerInvariant() : null;
        }
    }
}
