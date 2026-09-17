using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.ActivityLog;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoActivityLog
    {
        private readonly AccessDB accessDB;

        public DaoActivityLog(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetActivityLog()
        {
            string query = @"
                SELECT
                    al.activity_log_id,
                    al.user_id,
                    al.log_date,
                    al.action,
                    al.table_name,
                    al.record_id,
                    al.old_value,
                    al.new_value,
                    u.username AS user_name,
                    COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(u.first_name, ' ', u.last_name))), ''), u.username) AS user_display_name,
                    c.full_name AS client_full_name,
                    c.payment_identifier AS client_payment_identifier,
                    pc.full_name AS payment_client_full_name,
                    pc.payment_identifier AS payment_client_payment_identifier,
                    COALESCE(
                        p.amount,
                        CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(DECIMAL(18, 2), JSON_VALUE(al.new_value, '$.Amount')) END,
                        CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(DECIMAL(18, 2), JSON_VALUE(al.old_value, '$.Amount')) END
                    ) AS payment_amount,
                    COALESCE(
                        p.payment_date,
                        CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(DATETIME2, JSON_VALUE(al.new_value, '$.Date')) END,
                        CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(DATETIME2, JSON_VALUE(al.new_value, '$.PaymentDate')) END,
                        CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(DATETIME2, JSON_VALUE(al.old_value, '$.Date')) END,
                        CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(DATETIME2, JSON_VALUE(al.old_value, '$.PaymentDate')) END
                    ) AS payment_date
                FROM activity_log al
                LEFT JOIN users u ON u.user_id = al.user_id
                LEFT JOIN clients c ON c.client_id = al.record_id AND al.table_name = 'clients'
                LEFT JOIN payments p ON p.payment_id = al.record_id AND al.table_name = 'payments'
                LEFT JOIN rentals payment_rental ON payment_rental.rental_id = COALESCE(
                    CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.new_value, '$.RentalId')) END,
                    CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.old_value, '$.RentalId')) END
                ) AND al.table_name = 'payments'
                LEFT JOIN clients pc ON pc.client_id = COALESCE(
                    p.client_id,
                    CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.new_value, '$.ClientId')) END,
                    CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.old_value, '$.ClientId')) END,
                    payment_rental.client_id
                ) AND al.table_name = 'payments'
                ORDER BY al.log_date DESC, al.activity_log_id DESC";

            return await accessDB.GetTableAsync("activity_log", query);
        }

        public async Task<(DataTable Items, int TotalCount)> GetActivityLogPageAsync(ActivityLogFilterDto filter)
        {
            const string areaParameter = "@area";
            const string actionParameter = "@action";
            const string userIdParameter = "@user_id";
            const string fromDateParameter = "@from_date";
            const string toDateParameter = "@to_date";
            const string searchParameter = "@search";

            string whereClause = $@"
                WHERE (
                        {areaParameter} IS NULL
                        OR ({areaParameter} = 'users' AND al.table_name IN ('users', 'auth'))
                        OR al.table_name = {areaParameter}
                  )
                  AND ({actionParameter} IS NULL OR al.action = {actionParameter})
                  AND ({userIdParameter} IS NULL OR al.user_id = {userIdParameter})
                  AND ({fromDateParameter} IS NULL OR al.log_date >= {fromDateParameter})
                  AND ({toDateParameter} IS NULL OR al.log_date < {toDateParameter})
                  AND (
                        {searchParameter} IS NULL
                        OR ISNULL(u.username, '') LIKE {searchParameter}
                        OR ISNULL(u.first_name, '') LIKE {searchParameter}
                        OR ISNULL(u.last_name, '') LIKE {searchParameter}
                        OR al.action LIKE {searchParameter}
                        OR al.table_name LIKE {searchParameter}
                        OR CONVERT(VARCHAR(20), al.record_id) LIKE {searchParameter}
                        OR ISNULL(c.full_name, '') LIKE {searchParameter}
                        OR CONVERT(VARCHAR(50), c.payment_identifier) LIKE {searchParameter}
                        OR ISNULL(pc.full_name, '') LIKE {searchParameter}
                        OR CONVERT(VARCHAR(50), pc.payment_identifier) LIKE {searchParameter}
                  )";

            string query = $@"
                SELECT
                    al.activity_log_id,
                    al.user_id,
                    al.log_date,
                    al.action,
                    al.table_name,
                    al.record_id,
                    al.old_value,
                    al.new_value,
                    u.username AS user_name,
                    COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(u.first_name, ' ', u.last_name))), ''), u.username) AS user_display_name,
                    c.full_name AS client_full_name,
                    c.payment_identifier AS client_payment_identifier,
                    pc.full_name AS payment_client_full_name,
                    pc.payment_identifier AS payment_client_payment_identifier,
                    COALESCE(
                        p.amount,
                        CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(DECIMAL(18, 2), JSON_VALUE(al.new_value, '$.Amount')) END,
                        CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(DECIMAL(18, 2), JSON_VALUE(al.old_value, '$.Amount')) END
                    ) AS payment_amount,
                    COALESCE(
                        p.payment_date,
                        CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(DATETIME2, JSON_VALUE(al.new_value, '$.Date')) END,
                        CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(DATETIME2, JSON_VALUE(al.new_value, '$.PaymentDate')) END,
                        CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(DATETIME2, JSON_VALUE(al.old_value, '$.Date')) END,
                        CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(DATETIME2, JSON_VALUE(al.old_value, '$.PaymentDate')) END
                    ) AS payment_date
                FROM activity_log al
                LEFT JOIN users u ON u.user_id = al.user_id
                LEFT JOIN clients c ON c.client_id = al.record_id AND al.table_name = 'clients'
                LEFT JOIN payments p ON p.payment_id = al.record_id AND al.table_name = 'payments'
                LEFT JOIN rentals payment_rental ON payment_rental.rental_id = COALESCE(
                    CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.new_value, '$.RentalId')) END,
                    CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.old_value, '$.RentalId')) END
                ) AND al.table_name = 'payments'
                LEFT JOIN clients pc ON pc.client_id = COALESCE(
                    p.client_id,
                    CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.new_value, '$.ClientId')) END,
                    CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.old_value, '$.ClientId')) END,
                    payment_rental.client_id
                ) AND al.table_name = 'payments'
                {whereClause}
                ORDER BY al.log_date DESC, al.activity_log_id DESC
                OFFSET @offset ROWS FETCH NEXT @page_size ROWS ONLY";

            var pageParameters = BuildFilterParameters(filter).ToList();
            pageParameters.Add(new SqlParameter("@offset", SqlDbType.Int)
            {
                Value = (filter.PageNumber - 1) * filter.PageSize
            });
            pageParameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = filter.PageSize });

            DataTable items = await accessDB.GetTableAsync("activity_log_page", query, pageParameters.ToArray());

            string countQuery = $@"
                SELECT COUNT(1)
                FROM activity_log al
                LEFT JOIN users u ON u.user_id = al.user_id
                LEFT JOIN clients c ON c.client_id = al.record_id AND al.table_name = 'clients'
                LEFT JOIN payments p ON p.payment_id = al.record_id AND al.table_name = 'payments'
                LEFT JOIN rentals payment_rental ON payment_rental.rental_id = COALESCE(
                    CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.new_value, '$.RentalId')) END,
                    CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.old_value, '$.RentalId')) END
                ) AND al.table_name = 'payments'
                LEFT JOIN clients pc ON pc.client_id = COALESCE(
                    p.client_id,
                    CASE WHEN ISJSON(al.new_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.new_value, '$.ClientId')) END,
                    CASE WHEN ISJSON(al.old_value) = 1 THEN TRY_CONVERT(INT, JSON_VALUE(al.old_value, '$.ClientId')) END,
                    payment_rental.client_id
                ) AND al.table_name = 'payments'
                {whereClause}";

            object countResult = await accessDB.ExecuteScalarAsync(countQuery, BuildFilterParameters(filter));
            int totalCount = countResult == DBNull.Value ? 0 : Convert.ToInt32(countResult);

            return (items, totalCount);
        }

        public async Task<DataTable> GetActivityLogUsersAsync()
        {
            const string query = @"
                SELECT
                    user_id,
                    username,
                    COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(first_name, ' ', last_name))), ''), username) AS display_name
                FROM users
                ORDER BY display_name, username";

            return await accessDB.GetTableAsync("activity_log_users", query);
        }

        private static SqlParameter[] BuildFilterParameters(ActivityLogFilterDto filter)
        {
            DateTime? toDateExclusive = filter.ToDate?.Date.AddDays(1);
            string? normalizedSearch = string.IsNullOrWhiteSpace(filter.Search)
                ? null
                : $"%{filter.Search.Trim()}%";

            return
            [
                new SqlParameter("@area", SqlDbType.VarChar, 100) { Value = (object?)filter.Area ?? DBNull.Value },
                new SqlParameter("@action", SqlDbType.VarChar, 50) { Value = (object?)filter.Action ?? DBNull.Value },
                new SqlParameter("@user_id", SqlDbType.Int) { Value = (object?)filter.UserId ?? DBNull.Value },
                new SqlParameter("@from_date", SqlDbType.DateTime2) { Value = (object?)filter.FromDate?.Date ?? DBNull.Value },
                new SqlParameter("@to_date", SqlDbType.DateTime2) { Value = (object?)toDateExclusive ?? DBNull.Value },
                new SqlParameter("@search", SqlDbType.NVarChar, 250) { Value = (object?)normalizedSearch ?? DBNull.Value }
            ];
        }

        public async Task<DataTable> GetActivityLogsByUserId(int userId)
        {

            string query = @"
                SELECT
                    al.activity_log_id,
                    al.user_id,
                    al.log_date,
                    al.action,
                    al.table_name,
                    al.record_id,
                    al.old_value,
                    al.new_value,
                    c.full_name AS client_full_name,
                    c.payment_identifier AS client_payment_identifier
                FROM activity_log al
                LEFT JOIN clients c ON c.client_id = al.record_id AND al.table_name = 'clients'
                WHERE al.user_id = @user_id
                ORDER BY al.log_date DESC, al.activity_log_id DESC";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@user_id", SqlDbType.Int){Value  = userId},
            };

            return await accessDB.GetTableAsync("activity_log", query, parameters);
        }

        public async Task<bool> CreateActivityLog(ActivityLog activityLog)
        {

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

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
        
        public async Task<bool> DeleteActivityLog(int activityLogId)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@activity_log_id", SqlDbType.Int){Value = activityLogId},
            };

            string query = "DELETE FROM activity_log WHERE activity_log_id = @activity_log_id";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
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
