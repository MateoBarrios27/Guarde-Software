using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    public sealed class DaoNotification
    {
        private readonly AccessDB _accessDB;

        public DaoNotification(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        public Task<DataTable> GetInboxAsync(int userId, int take)
        {
            const string query = @"
                SELECT TOP (@Take)
                    n.notification_id,
                    n.source_type,
                    n.severity,
                    n.title,
                    n.message,
                    n.action_url,
                    n.created_at,
                    CAST(CASE WHEN nr.notification_id IS NULL THEN 0 ELSE 1 END AS bit) AS is_read
                FROM notifications n
                LEFT JOIN notification_reads nr
                    ON nr.notification_id = n.notification_id
                   AND nr.user_id = @UserId
                WHERE n.resolved_at IS NULL
                  AND (n.expires_at IS NULL OR n.expires_at > SYSDATETIME())
                  AND (n.target_user_id IS NULL OR n.target_user_id = @UserId)
                ORDER BY n.created_at DESC, n.notification_id DESC;";

            SqlParameter[] parameters =
            [
                new("@UserId", SqlDbType.Int) { Value = userId },
                new("@Take", SqlDbType.Int) { Value = Math.Clamp(take, 1, 100) }
            ];
            return _accessDB.GetTableAsync("notifications", query, parameters);
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            const string query = @"
                SELECT COUNT(1)
                FROM notifications n
                WHERE n.resolved_at IS NULL
                  AND (n.expires_at IS NULL OR n.expires_at > SYSDATETIME())
                  AND (n.target_user_id IS NULL OR n.target_user_id = @UserId)
                  AND NOT EXISTS (
                      SELECT 1 FROM notification_reads nr
                      WHERE nr.notification_id = n.notification_id AND nr.user_id = @UserId
                  );";
            return await ExecuteScalarIntAsync(query,
            [
                new("@UserId", SqlDbType.Int) { Value = userId }
            ]);
        }

        public Task<int> MarkAsReadAsync(long notificationId, int userId)
        {
            const string query = @"
                IF EXISTS (
                    SELECT 1 FROM notifications
                    WHERE notification_id = @NotificationId
                      AND resolved_at IS NULL
                      AND (expires_at IS NULL OR expires_at > SYSDATETIME())
                      AND (target_user_id IS NULL OR target_user_id = @UserId)
                )
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM notification_reads
                        WHERE notification_id = @NotificationId AND user_id = @UserId
                    )
                    BEGIN
                        INSERT INTO notification_reads (notification_id, user_id)
                        VALUES (@NotificationId, @UserId);
                    END;
                    SELECT 1;
                END
                ELSE SELECT 0;";

            return ExecuteScalarIntAsync(query,
            [
                new("@NotificationId", SqlDbType.BigInt) { Value = notificationId },
                new("@UserId", SqlDbType.Int) { Value = userId }
            ]);
        }

        public Task<int> MarkAllAsReadAsync(int userId)
        {
            const string query = @"
                INSERT INTO notification_reads (notification_id, user_id)
                SELECT n.notification_id, @UserId
                FROM notifications n
                WHERE n.resolved_at IS NULL
                  AND (n.expires_at IS NULL OR n.expires_at > SYSDATETIME())
                  AND (n.target_user_id IS NULL OR n.target_user_id = @UserId)
                  AND NOT EXISTS (
                      SELECT 1 FROM notification_reads nr
                      WHERE nr.notification_id = n.notification_id AND nr.user_id = @UserId
                  );
                SELECT @@ROWCOUNT;";

            return ExecuteScalarIntAsync(query,
            [
                new("@UserId", SqlDbType.Int) { Value = userId }
            ]);
        }

        public Task<long> CreateAsync(
            string sourceType,
            string severity,
            string title,
            string message,
            string? actionUrl = null,
            int? targetUserId = null,
            int? createdByUserId = null,
            string? notificationKey = null,
            DateTime? expiresAt = null)
        {
            const string query = @"
                INSERT INTO notifications (
                    notification_key, source_type, severity, title, message,
                    action_url, target_user_id, created_by_user_id, expires_at
                )
                OUTPUT INSERTED.notification_id
                VALUES (
                    @NotificationKey, @SourceType, @Severity, @Title, @Message,
                    @ActionUrl, @TargetUserId, @CreatedByUserId, @ExpiresAt
                );";

            return ExecuteScalarLongAsync(query,
            [
                StringParameter("@NotificationKey", 200, notificationKey),
                StringParameter("@SourceType", 50, sourceType),
                StringParameter("@Severity", 20, severity),
                StringParameter("@Title", 120, title),
                StringParameter("@Message", 600, message),
                StringParameter("@ActionUrl", 500, actionUrl),
                new("@TargetUserId", SqlDbType.Int) { Value = (object?)targetUserId ?? DBNull.Value },
                new("@CreatedByUserId", SqlDbType.Int) { Value = (object?)createdByUserId ?? DBNull.Value },
                new("@ExpiresAt", SqlDbType.DateTime2) { Value = (object?)expiresAt ?? DBNull.Value }
            ]);
        }

        public Task UpsertOperationalAsync(
            string notificationKey,
            string sourceType,
            string severity,
            string title,
            string message,
            string? actionUrl)
        {
            const string query = @"
                DECLARE @Touched TABLE (notification_id BIGINT, previous_resolved_at DATETIME2(0));

                MERGE notifications WITH (HOLDLOCK) AS target
                USING (SELECT @NotificationKey AS notification_key) AS source
                    ON target.notification_key = source.notification_key
                WHEN MATCHED THEN
                    UPDATE SET
                        source_type = @SourceType,
                        severity = @Severity,
                        title = @Title,
                        message = @Message,
                        action_url = @ActionUrl,
                        created_at = CASE WHEN target.resolved_at IS NOT NULL THEN SYSDATETIME() ELSE target.created_at END,
                        resolved_at = NULL
                WHEN NOT MATCHED THEN
                    INSERT (notification_key, source_type, severity, title, message, action_url)
                    VALUES (@NotificationKey, @SourceType, @Severity, @Title, @Message, @ActionUrl)
                OUTPUT inserted.notification_id, deleted.resolved_at
                    INTO @Touched (notification_id, previous_resolved_at);

                DELETE nr
                FROM notification_reads nr
                INNER JOIN @Touched touched ON touched.notification_id = nr.notification_id
                WHERE touched.previous_resolved_at IS NOT NULL;";

            return _accessDB.ExecuteCommandAsync(query,
            [
                StringParameter("@NotificationKey", 200, notificationKey),
                StringParameter("@SourceType", 50, sourceType),
                StringParameter("@Severity", 20, severity),
                StringParameter("@Title", 120, title),
                StringParameter("@Message", 600, message),
                StringParameter("@ActionUrl", 500, actionUrl)
            ]);
        }

        public Task ResolveByKeyAsync(string notificationKey)
        {
            const string query = @"
                UPDATE notifications
                SET resolved_at = COALESCE(resolved_at, SYSDATETIME())
                WHERE notification_key = @NotificationKey;";
            return _accessDB.ExecuteCommandAsync(query,
            [
                StringParameter("@NotificationKey", 200, notificationKey)
            ]);
        }

        public Task SyncClientIncreaseNotificationsAsync(DateTime today, string nextMonthLabel)
        {
            const string query = @"
                DECLARE @Due TABLE (
                    notification_key NVARCHAR(200) NOT NULL PRIMARY KEY,
                    client_id INT NOT NULL,
                    full_name NVARCHAR(255) NOT NULL
                );

                IF DAY(@Today) >= 25
                BEGIN
                    INSERT INTO @Due (notification_key, client_id, full_name)
                    SELECT DISTINCT
                        N'client-increase:' + CONVERT(nvarchar(20), c.client_id) + N':'
                            + CONVERT(char(7), DATEADD(month, 1, @Today), 126),
                        c.client_id,
                        c.full_name
                    FROM clients c
                    INNER JOIN rentals r ON r.client_id = c.client_id AND r.active = 1
                    WHERE c.active = 1
                      AND ISNULL(c.is_deleted, 0) = 0
                      AND r.increase_anchor_date >= DATEFROMPARTS(YEAR(DATEADD(month, 1, @Today)), MONTH(DATEADD(month, 1, @Today)), 1)
                      AND r.increase_anchor_date < DATEADD(month, 1, DATEFROMPARTS(YEAR(DATEADD(month, 1, @Today)), MONTH(DATEADD(month, 1, @Today)), 1))
                      AND (r.price_lock_end_date IS NULL OR r.price_lock_end_date < r.increase_anchor_date)
                      AND (
                          r.months_unpaid > 0
                          OR EXISTS (
                              SELECT 1
                              FROM client_month_balances cmb
                              WHERE cmb.rental_id = r.rental_id
                                AND cmb.month_year = RIGHT('0' + CONVERT(varchar(2), MONTH(@Today)), 2) + '/' + CONVERT(varchar(4), YEAR(@Today))
                                AND ISNULL(cmb.balance, 0) - ISNULL(cmb.paid, 0) - ISNULL(cmb.advanced_payment, 0) > 0
                          )
                      );
                END;

                DECLARE @Touched TABLE (notification_id BIGINT, previous_resolved_at DATETIME2(0));

                MERGE notifications WITH (HOLDLOCK) AS target
                USING @Due AS source
                    ON target.notification_key = source.notification_key
                WHEN MATCHED THEN
                    UPDATE SET
                        source_type = N'client_increase_due',
                        severity = N'warning',
                        title = N'Asignar el próximo abono',
                        message = source.full_name + N' sigue impago y tiene aumento en ' + @NextMonthLabel
                            + N'. Asignalo antes del cierre del mes para proyectar y debitar correctamente.',
                        action_url = N'/clients?detailClientId=' + CONVERT(nvarchar(20), source.client_id),
                        created_at = CASE WHEN target.resolved_at IS NOT NULL THEN SYSDATETIME() ELSE target.created_at END,
                        resolved_at = NULL
                WHEN NOT MATCHED THEN
                    INSERT (notification_key, source_type, severity, title, message, action_url)
                    VALUES (
                        source.notification_key,
                        N'client_increase_due',
                        N'warning',
                        N'Asignar el próximo abono',
                        source.full_name + N' sigue impago y tiene aumento en ' + @NextMonthLabel
                            + N'. Asignalo antes del cierre del mes para proyectar y debitar correctamente.',
                        N'/clients?detailClientId=' + CONVERT(nvarchar(20), source.client_id)
                    )
                OUTPUT inserted.notification_id, deleted.resolved_at
                    INTO @Touched (notification_id, previous_resolved_at);

                DELETE nr
                FROM notification_reads nr
                INNER JOIN @Touched touched ON touched.notification_id = nr.notification_id
                WHERE touched.previous_resolved_at IS NOT NULL;

                UPDATE n
                SET resolved_at = SYSDATETIME()
                FROM notifications n
                WHERE n.source_type = N'client_increase_due'
                  AND n.resolved_at IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM @Due due WHERE due.notification_key = n.notification_key
                  );";

            return _accessDB.ExecuteCommandAsync(query,
            [
                new("@Today", SqlDbType.Date) { Value = today.Date },
                StringParameter("@NextMonthLabel", 80, nextMonthLabel)
            ]);
        }

        public Task<bool> HasMonthlyIncreaseAsync(DateTime month)
        {
            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM monthly_increase_settings
                    WHERE effective_date = DATEFROMPARTS(YEAR(@Month), MONTH(@Month), 1)
                ) THEN 1 ELSE 0 END;";
            return ExecuteScalarBoolAsync(query,
            [
                new("@Month", SqlDbType.Date) { Value = month.Date }
            ]);
        }

        private async Task<int> ExecuteScalarIntAsync(string query, SqlParameter[] parameters)
        {
            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private async Task<long> ExecuteScalarLongAsync(string query, SqlParameter[] parameters)
        {
            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToInt64(result);
        }

        private async Task<bool> ExecuteScalarBoolAsync(string query, SqlParameter[] parameters)
        {
            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return result != DBNull.Value && Convert.ToBoolean(result);
        }

        private static SqlParameter StringParameter(string name, int size, string? value)
            => new(name, SqlDbType.NVarChar, size) { Value = (object?)value ?? DBNull.Value };
    }
}
