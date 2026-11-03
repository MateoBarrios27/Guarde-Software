using System.Data;
using GuardeSoftwareAPI.Dtos.Chat;
using GuardeSoftwareAPI.Services.chat;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    /// <summary>Fila de conversación con lo necesario para autenticar al visitante.</summary>
    public sealed class ChatConversationRecord
    {
        public long Id { get; init; }
        public Guid PublicId { get; init; }
        public byte[] TokenHash { get; init; } = [];
        public string Status { get; init; } = "open";
        public string VisitorName { get; init; } = string.Empty;
        public string? VisitorEmail { get; init; }
        public string? VisitorPhone { get; init; }
    }

    public sealed class ChatInboxCounts
    {
        public int AwaitingReplyCount { get; init; }
        public int UnreadConversationCount { get; init; }
        public DateTime? OldestAwaitingSince { get; init; }
    }

    /// <summary>
    /// Acceso a datos del chat en vivo. Cada escritura es un único lote con
    /// XACT_ABORT: el mensaje y el estado de la conversación cambian juntos o no cambian.
    /// Todas las marcas de tiempo son UTC y las aporta la aplicación.
    /// </summary>
    public sealed class DaoChat
    {
        private const string StaffNameSql =
            "COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(u.first_name, N' ', u.last_name))), N''), u.username, N'Administración')";

        // "Sin responder" = el último mensaje del visitante es posterior a la última respuesta.
        private const string AwaitingPredicate =
            "c.status = N'open' AND ISNULL(c.last_visitor_message_id, 0) > ISNULL(c.last_staff_message_id, 0)";

        private const string SummarySelect = @"
            SELECT
                c.conversation_id, c.public_id, c.visitor_name, c.visitor_email, c.visitor_phone,
                c.source_url, c.status, c.created_at, c.last_message_at,
                LEFT(lm.body, 140) AS last_body, lm.sender_type AS last_sender,
                CAST(CASE WHEN c.status = N'open'
                               AND ISNULL(c.last_visitor_message_id, 0) > ISNULL(c.last_staff_message_id, 0)
                          THEN 1 ELSE 0 END AS bit) AS awaiting_reply,
                aw.since AS awaiting_since,
                CAST(ISNULL(aw.ooh, 0) AS bit) AS awaiting_out_of_hours,
                CASE WHEN ISNULL(c.last_visitor_message_id, 0) > c.staff_last_read_message_id
                     THEN (SELECT COUNT(1) FROM dbo.chat_messages m
                           WHERE m.conversation_id = c.conversation_id
                             AND m.sender_type = N'visitor'
                             AND m.message_id > c.staff_last_read_message_id)
                     ELSE 0 END AS staff_unread_count,
                CASE WHEN u.user_id IS NULL THEN NULL ELSE " + StaffNameSql + @" END AS assigned_name
            FROM dbo.chat_conversations c
            LEFT JOIN dbo.users u ON u.user_id = c.assigned_user_id
            OUTER APPLY (
                SELECT TOP (1) m.body, m.sender_type
                FROM dbo.chat_messages m
                WHERE m.conversation_id = c.conversation_id
                ORDER BY m.message_id DESC
            ) lm
            OUTER APPLY (
                SELECT MIN(m.created_at) AS since,
                       MAX(CAST(m.received_out_of_hours AS int)) AS ooh
                FROM dbo.chat_messages m
                WHERE m.conversation_id = c.conversation_id
                  AND m.sender_type = N'visitor'
                  AND m.message_id > ISNULL(c.last_staff_message_id, 0)
            ) aw";

        private readonly AccessDB _accessDB;

        public DaoChat(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        // ---------------------------------------------------------------- horario

        public async Task<List<ChatBusinessDay>> GetScheduleAsync()
        {
            DataTable table = await _accessDB.GetTableAsync(
                "chat_business_hours",
                "SELECT day_of_week, is_open, opens_at, closes_at FROM dbo.chat_business_hours ORDER BY day_of_week;");

            var days = new List<ChatBusinessDay>(table.Rows.Count);
            foreach (DataRow row in table.Rows)
            {
                bool isOpen = Convert.ToBoolean(row["is_open"]);
                days.Add(new ChatBusinessDay(
                    Convert.ToInt32(row["day_of_week"]),
                    isOpen,
                    isOpen && row["opens_at"] is TimeSpan opens ? opens : null,
                    isOpen && row["closes_at"] is TimeSpan closes ? closes : null));
            }

            return days;
        }

        public Task SaveScheduleAsync(IReadOnlyList<ChatBusinessDay> days, int userId, DateTime nowUtc)
        {
            var values = new List<string>(days.Count);
            var parameters = new List<SqlParameter>
            {
                new("@UserId", SqlDbType.Int) { Value = userId },
                new("@Now", SqlDbType.DateTime2) { Value = nowUtc }
            };

            for (int index = 0; index < days.Count; index++)
            {
                ChatBusinessDay day = days[index];
                values.Add($"({day.DayOfWeek}, @Open{index}, @Opens{index}, @Closes{index})");
                parameters.Add(new SqlParameter($"@Open{index}", SqlDbType.Bit) { Value = day.IsOpen });
                parameters.Add(new SqlParameter($"@Opens{index}", SqlDbType.Time) { Value = (object?)day.OpensAt ?? DBNull.Value });
                parameters.Add(new SqlParameter($"@Closes{index}", SqlDbType.Time) { Value = (object?)day.ClosesAt ?? DBNull.Value });
            }

            string query = $@"
                SET XACT_ABORT ON;
                MERGE dbo.chat_business_hours AS target
                USING (VALUES {string.Join(", ", values)}) AS source (day_of_week, is_open, opens_at, closes_at)
                    ON target.day_of_week = source.day_of_week
                WHEN MATCHED THEN UPDATE SET
                    is_open = source.is_open, opens_at = source.opens_at, closes_at = source.closes_at,
                    updated_at = @Now, updated_by_user_id = @UserId
                WHEN NOT MATCHED THEN INSERT (day_of_week, is_open, opens_at, closes_at, updated_at, updated_by_user_id)
                    VALUES (source.day_of_week, source.is_open, source.opens_at, source.closes_at, @Now, @UserId);";

            return _accessDB.ExecuteCommandAsync(query, parameters.ToArray());
        }

        // ---------------------------------------------------------- conversaciones

        public async Task<(long ConversationId, Guid PublicId, long MessageId)> CreateConversationAsync(
            byte[] tokenHash,
            string visitorName,
            string? visitorEmail,
            string? visitorPhone,
            string? sourceUrl,
            string firstMessage,
            bool outOfHours,
            DateTime nowUtc)
        {
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                DECLARE @PublicId UNIQUEIDENTIFIER = NEWID();
                DECLARE @ConversationId BIGINT;
                DECLARE @MessageId BIGINT;

                INSERT INTO dbo.chat_conversations
                    (public_id, visitor_token_hash, visitor_name, visitor_email, visitor_phone,
                     source_url, status, created_at, last_message_at, visitor_last_seen_at)
                VALUES
                    (@PublicId, @TokenHash, @Name, @Email, @Phone,
                     @SourceUrl, N'open', @Now, @Now, @Now);
                SET @ConversationId = SCOPE_IDENTITY();

                INSERT INTO dbo.chat_messages
                    (conversation_id, sender_type, body, created_at, received_out_of_hours)
                VALUES (@ConversationId, N'visitor', @Body, @Now, @OutOfHours);
                SET @MessageId = SCOPE_IDENTITY();

                UPDATE dbo.chat_conversations
                SET last_visitor_message_id = @MessageId
                WHERE conversation_id = @ConversationId;
                COMMIT TRANSACTION;

                SELECT @ConversationId AS conversation_id, @PublicId AS public_id, @MessageId AS message_id;";

            DataTable table = await _accessDB.GetTableAsync("chat_create", query,
            [
                new("@TokenHash", SqlDbType.Binary, 32) { Value = tokenHash },
                new("@Name", SqlDbType.NVarChar, 100) { Value = visitorName },
                new("@Email", SqlDbType.NVarChar, 254) { Value = (object?)visitorEmail ?? DBNull.Value },
                new("@Phone", SqlDbType.NVarChar, 40) { Value = (object?)visitorPhone ?? DBNull.Value },
                new("@SourceUrl", SqlDbType.NVarChar, 500) { Value = (object?)sourceUrl ?? DBNull.Value },
                new("@Body", SqlDbType.NVarChar, 2000) { Value = firstMessage },
                new("@OutOfHours", SqlDbType.Bit) { Value = outOfHours },
                new("@Now", SqlDbType.DateTime2) { Value = nowUtc }
            ]);

            DataRow row = table.Rows[0];
            return (Convert.ToInt64(row["conversation_id"]), (Guid)row["public_id"], Convert.ToInt64(row["message_id"]));
        }

        public async Task<ChatConversationRecord?> FindByPublicIdAsync(Guid publicId)
        {
            DataTable table = await _accessDB.GetTableAsync("chat_find", @"
                SELECT conversation_id, public_id, visitor_token_hash, status,
                       visitor_name, visitor_email, visitor_phone
                FROM dbo.chat_conversations
                WHERE public_id = @PublicId;",
            [
                new("@PublicId", SqlDbType.UniqueIdentifier) { Value = publicId }
            ]);

            return table.Rows.Count == 0 ? null : MapRecord(table.Rows[0]);
        }

        public async Task<ChatConversationRecord?> FindByIdAsync(long conversationId)
        {
            DataTable table = await _accessDB.GetTableAsync("chat_find", @"
                SELECT conversation_id, public_id, visitor_token_hash, status,
                       visitor_name, visitor_email, visitor_phone
                FROM dbo.chat_conversations
                WHERE conversation_id = @Id;",
            [
                new("@Id", SqlDbType.BigInt) { Value = conversationId }
            ]);

            return table.Rows.Count == 0 ? null : MapRecord(table.Rows[0]);
        }

        public async Task<ChatConversationSummaryDto?> GetSummaryAsync(long conversationId)
        {
            DataTable table = await _accessDB.GetTableAsync(
                "chat_summary",
                SummarySelect + " WHERE c.conversation_id = @Id;",
                [new("@Id", SqlDbType.BigInt) { Value = conversationId }]);

            return table.Rows.Count == 0 ? null : MapSummary(table.Rows[0]);
        }

        public async Task<List<ChatConversationSummaryDto>> ListAsync(string? status, string? search, int take)
        {
            string? pattern = string.IsNullOrWhiteSpace(search) ? null : $"%{EscapeLike(search.Trim())}%";

            // Sin responder primero (la espera más larga arriba) y después lo más reciente.
            string query = $@"
                SELECT TOP (@Take) q.*
                FROM ({SummarySelect}
                      WHERE (@Status IS NULL OR c.status = @Status)
                        AND (@Search IS NULL
                             OR c.visitor_name LIKE @Search ESCAPE N'\'
                             OR c.visitor_email LIKE @Search ESCAPE N'\'
                             OR c.visitor_phone LIKE @Search ESCAPE N'\')) q
                ORDER BY CASE WHEN q.awaiting_reply = 1 THEN 0 ELSE 1 END,
                         CASE WHEN q.awaiting_reply = 1 THEN q.awaiting_since END ASC,
                         q.last_message_at DESC,
                         q.conversation_id DESC;";

            DataTable table = await _accessDB.GetTableAsync("chat_list", query,
            [
                new("@Take", SqlDbType.Int) { Value = Math.Clamp(take, 1, 300) },
                new("@Status", SqlDbType.NVarChar, 10) { Value = (object?)status ?? DBNull.Value },
                new("@Search", SqlDbType.NVarChar, 200) { Value = (object?)pattern ?? DBNull.Value }
            ]);

            return table.Rows.Cast<DataRow>().Select(MapSummary).ToList();
        }

        public async Task<ChatInboxCounts> GetInboxCountsAsync()
        {
            string query = $@"
                SELECT
                    (SELECT COUNT(1) FROM dbo.chat_conversations c WHERE {AwaitingPredicate}) AS awaiting_count,
                    (SELECT COUNT(1) FROM dbo.chat_conversations c
                      WHERE c.status = N'open'
                        AND ISNULL(c.last_visitor_message_id, 0) > c.staff_last_read_message_id) AS unread_count,
                    (SELECT MIN(aw.since)
                       FROM dbo.chat_conversations c
                       OUTER APPLY (
                           SELECT MIN(m.created_at) AS since
                           FROM dbo.chat_messages m
                           WHERE m.conversation_id = c.conversation_id
                             AND m.sender_type = N'visitor'
                             AND m.message_id > ISNULL(c.last_staff_message_id, 0)) aw
                      WHERE {AwaitingPredicate}) AS oldest_awaiting;";

            DataTable table = await _accessDB.GetTableAsync("chat_counts", query);
            DataRow row = table.Rows[0];
            return new ChatInboxCounts
            {
                AwaitingReplyCount = Convert.ToInt32(row["awaiting_count"]),
                UnreadConversationCount = Convert.ToInt32(row["unread_count"]),
                OldestAwaitingSince = ToUtc(row["oldest_awaiting"])
            };
        }

        // ---------------------------------------------------------------- mensajes

        public async Task<List<ChatMessageDto>> GetMessagesAsync(long conversationId, long afterMessageId, int take)
        {
            string query = $@"
                SELECT x.*
                FROM (
                    SELECT TOP (@Take)
                        m.message_id, m.sender_type, m.body, m.created_at,
                        m.received_out_of_hours, m.email_status,
                        CASE WHEN m.sender_type = N'visitor' THEN c.visitor_name ELSE {StaffNameSql} END AS sender_name
                    FROM dbo.chat_messages m
                    JOIN dbo.chat_conversations c ON c.conversation_id = m.conversation_id
                    LEFT JOIN dbo.users u ON u.user_id = m.sender_user_id
                    WHERE m.conversation_id = @ConversationId AND m.message_id > @After
                    ORDER BY m.message_id DESC
                ) x
                ORDER BY x.message_id ASC;";

            DataTable table = await _accessDB.GetTableAsync("chat_messages", query,
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@After", SqlDbType.BigInt) { Value = Math.Max(0, afterMessageId) },
                new("@Take", SqlDbType.Int) { Value = Math.Clamp(take, 1, 500) }
            ]);

            return table.Rows.Cast<DataRow>().Select(MapMessage).ToList();
        }

        public async Task<ChatMessageDto?> GetMessageAsync(long messageId)
        {
            string query = $@"
                SELECT m.message_id, m.sender_type, m.body, m.created_at,
                       m.received_out_of_hours, m.email_status,
                       CASE WHEN m.sender_type = N'visitor' THEN c.visitor_name ELSE {StaffNameSql} END AS sender_name
                FROM dbo.chat_messages m
                JOIN dbo.chat_conversations c ON c.conversation_id = m.conversation_id
                LEFT JOIN dbo.users u ON u.user_id = m.sender_user_id
                WHERE m.message_id = @MessageId;";

            DataTable table = await _accessDB.GetTableAsync("chat_message", query,
            [
                new("@MessageId", SqlDbType.BigInt) { Value = messageId }
            ]);

            return table.Rows.Count == 0 ? null : MapMessage(table.Rows[0]);
        }

        public async Task<long> AddVisitorMessageAsync(long conversationId, string body, bool outOfHours, DateTime nowUtc)
        {
            // Escribir reabre una conversación cerrada. Quien escribe está mirando el chat,
            // por eso también da por vistas las respuestas anteriores.
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                DECLARE @Id BIGINT;
                INSERT INTO dbo.chat_messages (conversation_id, sender_type, body, created_at, received_out_of_hours)
                VALUES (@ConversationId, N'visitor', @Body, @Now, @OutOfHours);
                SET @Id = SCOPE_IDENTITY();

                UPDATE dbo.chat_conversations
                SET last_message_at = @Now,
                    last_visitor_message_id = CASE WHEN ISNULL(last_visitor_message_id, 0) < @Id THEN @Id ELSE last_visitor_message_id END,
                    visitor_last_read_message_id = CASE WHEN visitor_last_read_message_id < ISNULL(last_staff_message_id, 0)
                                                        THEN ISNULL(last_staff_message_id, 0) ELSE visitor_last_read_message_id END,
                    visitor_last_seen_at = @Now,
                    status = N'open', closed_at = NULL, closed_by_user_id = NULL
                WHERE conversation_id = @ConversationId;
                COMMIT TRANSACTION;

                SELECT @Id;";

            object result = await _accessDB.ExecuteScalarAsync(query,
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@Body", SqlDbType.NVarChar, 2000) { Value = body },
                new("@OutOfHours", SqlDbType.Bit) { Value = outOfHours },
                new("@Now", SqlDbType.DateTime2) { Value = nowUtc }
            ]);

            return Convert.ToInt64(result);
        }

        public async Task<long> AddStaffMessageAsync(long conversationId, int userId, string body, DateTime nowUtc)
        {
            // Responder da por leído todo lo anterior y deja la conversación al día.
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                DECLARE @Id BIGINT;
                INSERT INTO dbo.chat_messages (conversation_id, sender_type, sender_user_id, body, created_at)
                VALUES (@ConversationId, N'staff', @UserId, @Body, @Now);
                SET @Id = SCOPE_IDENTITY();

                UPDATE dbo.chat_conversations
                SET last_message_at = @Now,
                    last_staff_message_id = CASE WHEN ISNULL(last_staff_message_id, 0) < @Id THEN @Id ELSE last_staff_message_id END,
                    staff_last_read_message_id = CASE WHEN staff_last_read_message_id < @Id THEN @Id ELSE staff_last_read_message_id END,
                    assigned_user_id = ISNULL(assigned_user_id, @UserId)
                WHERE conversation_id = @ConversationId;
                COMMIT TRANSACTION;

                SELECT @Id;";

            object result = await _accessDB.ExecuteScalarAsync(query,
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@UserId", SqlDbType.Int) { Value = userId },
                new("@Body", SqlDbType.NVarChar, 2000) { Value = body },
                new("@Now", SqlDbType.DateTime2) { Value = nowUtc }
            ]);

            return Convert.ToInt64(result);
        }

        public Task SetEmailStatusAsync(long messageId, string status, DateTime nowUtc)
        {
            return _accessDB.ExecuteCommandAsync(@"
                UPDATE dbo.chat_messages
                SET email_status = @Status,
                    email_sent_at = CASE WHEN @Status = N'sent' THEN @Now ELSE email_sent_at END
                WHERE message_id = @MessageId;",
            [
                new("@MessageId", SqlDbType.BigInt) { Value = messageId },
                new("@Status", SqlDbType.NVarChar, 20) { Value = status },
                new("@Now", SqlDbType.DateTime2) { Value = nowUtc }
            ]);
        }

        // -------------------------------------------------------- lectura y estado

        public Task MarkStaffReadAsync(long conversationId, long? upToMessageId)
        {
            // Nunca retrocede y nunca pasa del último mensaje existente.
            return _accessDB.ExecuteCommandAsync(@"
                UPDATE c
                SET staff_last_read_message_id = CASE
                    WHEN c.staff_last_read_message_id < t.target THEN t.target ELSE c.staff_last_read_message_id END
                FROM dbo.chat_conversations c
                CROSS APPLY (
                    SELECT CASE WHEN @UpTo IS NULL THEN ISNULL(MAX(m.message_id), 0)
                                ELSE ISNULL(MAX(CASE WHEN m.message_id <= @UpTo THEN m.message_id END), 0) END AS target
                    FROM dbo.chat_messages m
                    WHERE m.conversation_id = c.conversation_id) t
                WHERE c.conversation_id = @ConversationId;",
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@UpTo", SqlDbType.BigInt) { Value = (object?)upToMessageId ?? DBNull.Value }
            ]);
        }

        public Task MarkVisitorReadAsync(long conversationId, long? upToMessageId, DateTime nowUtc)
        {
            return _accessDB.ExecuteCommandAsync(@"
                UPDATE c
                SET visitor_last_read_message_id = CASE
                        WHEN c.visitor_last_read_message_id < t.target THEN t.target ELSE c.visitor_last_read_message_id END,
                    visitor_last_seen_at = @Now
                FROM dbo.chat_conversations c
                CROSS APPLY (
                    SELECT CASE WHEN @UpTo IS NULL THEN ISNULL(MAX(m.message_id), 0)
                                ELSE ISNULL(MAX(CASE WHEN m.message_id <= @UpTo THEN m.message_id END), 0) END AS target
                    FROM dbo.chat_messages m
                    WHERE m.conversation_id = c.conversation_id) t
                WHERE c.conversation_id = @ConversationId;",
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@UpTo", SqlDbType.BigInt) { Value = (object?)upToMessageId ?? DBNull.Value },
                new("@Now", SqlDbType.DateTime2) { Value = nowUtc }
            ]);
        }

        public Task TouchVisitorAsync(long conversationId, DateTime nowUtc)
        {
            return _accessDB.ExecuteCommandAsync(
                "UPDATE dbo.chat_conversations SET visitor_last_seen_at = @Now WHERE conversation_id = @ConversationId;",
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@Now", SqlDbType.DateTime2) { Value = nowUtc }
            ]);
        }

        public async Task<bool> CloseAsync(long conversationId, int userId, DateTime nowUtc)
        {
            int rows = await _accessDB.ExecuteCommandAsync(@"
                UPDATE dbo.chat_conversations
                SET status = N'closed', closed_at = @Now, closed_by_user_id = @UserId,
                    staff_last_read_message_id = CASE WHEN staff_last_read_message_id < ISNULL(last_visitor_message_id, 0)
                                                      THEN ISNULL(last_visitor_message_id, 0) ELSE staff_last_read_message_id END
                WHERE conversation_id = @ConversationId AND status = N'open';",
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@UserId", SqlDbType.Int) { Value = userId },
                new("@Now", SqlDbType.DateTime2) { Value = nowUtc }
            ]);
            return rows > 0;
        }

        public async Task<bool> ReopenAsync(long conversationId)
        {
            int rows = await _accessDB.ExecuteCommandAsync(@"
                UPDATE dbo.chat_conversations
                SET status = N'open', closed_at = NULL, closed_by_user_id = NULL
                WHERE conversation_id = @ConversationId AND status = N'closed';",
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId }
            ]);
            return rows > 0;
        }

        // --------------------------------------------------------- límites de uso

        /// <summary>Respuestas de la administración que el visitante todavía no vio: el globo del widget.</summary>
        public async Task<int> CountVisitorUnreadAsync(long conversationId)
        {
            object result = await _accessDB.ExecuteScalarAsync(@"
                SELECT COUNT(1)
                FROM dbo.chat_messages m
                JOIN dbo.chat_conversations c ON c.conversation_id = m.conversation_id
                WHERE m.conversation_id = @ConversationId
                  AND m.sender_type = N'staff'
                  AND m.message_id > c.visitor_last_read_message_id;",
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId }
            ]);
            return Convert.ToInt32(result);
        }

        public async Task<int> CountVisitorMessagesSinceAsync(long conversationId, DateTime sinceUtc)
        {
            object result = await _accessDB.ExecuteScalarAsync(@"
                SELECT COUNT(1) FROM dbo.chat_messages
                WHERE conversation_id = @ConversationId AND sender_type = N'visitor' AND created_at >= @Since;",
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@Since", SqlDbType.DateTime2) { Value = sinceUtc }
            ]);
            return Convert.ToInt32(result);
        }

        public async Task<int> CountRecentConversationsAsync(string? email, string? phone, DateTime sinceUtc)
        {
            object result = await _accessDB.ExecuteScalarAsync(@"
                SELECT COUNT(1) FROM dbo.chat_conversations
                WHERE created_at >= @Since
                  AND ((@Email IS NOT NULL AND visitor_email = @Email)
                    OR (@Phone IS NOT NULL AND visitor_phone = @Phone));",
            [
                new("@Since", SqlDbType.DateTime2) { Value = sinceUtc },
                new("@Email", SqlDbType.NVarChar, 254) { Value = (object?)email ?? DBNull.Value },
                new("@Phone", SqlDbType.NVarChar, 40) { Value = (object?)phone ?? DBNull.Value }
            ]);
            return Convert.ToInt32(result);
        }

        public async Task<int> CountEmailsSentSinceAsync(long conversationId, DateTime sinceUtc)
        {
            object result = await _accessDB.ExecuteScalarAsync(@"
                SELECT COUNT(1) FROM dbo.chat_messages
                WHERE conversation_id = @ConversationId AND email_status = N'sent' AND email_sent_at >= @Since;",
            [
                new("@ConversationId", SqlDbType.BigInt) { Value = conversationId },
                new("@Since", SqlDbType.DateTime2) { Value = sinceUtc }
            ]);
            return Convert.ToInt32(result);
        }

        // ------------------------------------------------------------------ mapeo

        private static ChatConversationRecord MapRecord(DataRow row) => new()
        {
            Id = Convert.ToInt64(row["conversation_id"]),
            PublicId = (Guid)row["public_id"],
            TokenHash = (byte[])row["visitor_token_hash"],
            Status = row["status"]?.ToString() ?? "open",
            VisitorName = row["visitor_name"]?.ToString() ?? string.Empty,
            VisitorEmail = NullableString(row["visitor_email"]),
            VisitorPhone = NullableString(row["visitor_phone"])
        };

        private static ChatConversationSummaryDto MapSummary(DataRow row) => new()
        {
            Id = Convert.ToInt64(row["conversation_id"]),
            PublicId = (Guid)row["public_id"],
            VisitorName = row["visitor_name"]?.ToString() ?? string.Empty,
            VisitorEmail = NullableString(row["visitor_email"]),
            VisitorPhone = NullableString(row["visitor_phone"]),
            SourceUrl = NullableString(row["source_url"]),
            Status = row["status"]?.ToString() ?? "open",
            CreatedAt = ToUtc(row["created_at"]) ?? default,
            LastMessageAt = ToUtc(row["last_message_at"]) ?? default,
            LastMessagePreview = row["last_body"]?.ToString() ?? string.Empty,
            LastMessageSender = NullableString(row["last_sender"]),
            AwaitingReply = Convert.ToBoolean(row["awaiting_reply"]),
            AwaitingReplySince = ToUtc(row["awaiting_since"]),
            AwaitingOutOfHours = Convert.ToBoolean(row["awaiting_out_of_hours"]),
            UnreadCount = Convert.ToInt32(row["staff_unread_count"]),
            AssignedUserName = NullableString(row["assigned_name"])
        };

        private static ChatMessageDto MapMessage(DataRow row) => new()
        {
            Id = Convert.ToInt64(row["message_id"]),
            SenderType = row["sender_type"]?.ToString() ?? string.Empty,
            SenderName = row["sender_name"]?.ToString() ?? string.Empty,
            Body = row["body"]?.ToString() ?? string.Empty,
            CreatedAt = ToUtc(row["created_at"]) ?? default,
            ReceivedOutOfHours = Convert.ToBoolean(row["received_out_of_hours"]),
            EmailStatus = NullableString(row["email_status"])
        };

        private static string? NullableString(object value)
            => value == DBNull.Value ? null : value?.ToString();

        private static DateTime? ToUtc(object value)
            => value is DateTime date ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null;

        private static string EscapeLike(string value)
            => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
    }
}
