using GuardeSoftwareAPI.Dtos.Communication;

using GuardeSoftwareAPI.Dao;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace GuardeSoftwareAPI.Dao
{
    public class CommunicationDao
    {
        private readonly AccessDB _accessDB;

        public CommunicationDao(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        // --- UPDATED to new schema ---
        private const string GET_COMMUNICATIONS_QUERY = @"
            SELECT 
                c.communication_id AS Id,
                c.title AS Title,
                (SELECT TOP 1 content FROM communication_channel_content WHERE communication_id = c.communication_id) AS Content,
                FORMAT(c.scheduled_date, 'yyyy-MM-dd') AS SendDate,
                FORMAT(c.scheduled_date, 'HH:mm') AS SendTime,
                c.status AS Status,
                FORMAT(c.creation_date, 'yyyy-MM-dd') AS CreationDate,
                
                (SELECT STRING_AGG(chan.name, ' + ') 
                 FROM communication_channel_content ccc
                 JOIN communication_channels chan ON ccc.channel_id = chan.channel_id
                 WHERE ccc.communication_id = c.communication_id) AS Channel,
                
                -- Fixed to use STRING_AGG for simplicity
                ISNULL(
                    (SELECT STRING_AGG(cl.first_name + ' ' + cl.last_name, ',')
                     FROM communication_recipients cr
                     JOIN clients cl ON cr.client_id = cl.client_id
                     WHERE cr.communication_id = c.communication_id), 
                '') AS RecipientsCsv
            FROM 
                communications c
        ";
        
        public async Task<List<CommunicationDto>> GetCommunicationsAsync()
        {
            var communications = new List<CommunicationDto>();
            string query = $"{GET_COMMUNICATIONS_QUERY} ORDER BY c.creation_date DESC;";
            DataTable table = await _accessDB.GetTableAsync("Communications", query);

            foreach (DataRow row in table.Rows)
            {
                communications.Add(MapDataRowToDto(row));
            }
            return communications;
        }

        public async Task<CommunicationDto> GetCommunicationByIdAsync(int id)
        {
            string query = $"{GET_COMMUNICATIONS_QUERY} WHERE c.communication_id = @Id;";
            var parameters = new[] { new SqlParameter("@Id", id) };
            
            DataTable table = await _accessDB.GetTableAsync("Communication", query, parameters);

            if (table.Rows.Count == 0)
            {
                throw new Exception($"Communication with ID {id} not found.");
            }
            return MapDataRowToDto(table.Rows[0]);
        }

        #region Transactional Methods (Updated)

        // --- UPDATED to new schema ---
        public async Task<int> InsertCommunicationAsync(UpsertCommunicationRequest request, int userId, DateTime? scheduledAt, string status, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO communications (creator_user_id, title, creation_date, scheduled_date, status)
                OUTPUT INSERTED.communication_id
                VALUES (@CreatorUserId, @Title, GETDATE(), @ScheduledDate, @Status);";
            
            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@CreatorUserId", userId);
                command.Parameters.AddWithValue("@Title", request.Title);
                command.Parameters.AddWithValue("@ScheduledDate", (object)scheduledAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@Status", status); // 'Draft' or 'Scheduled'

                object result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        // --- UPDATED to new schema ---
        public async Task<bool> InsertCommunicationChannelAsync(int communicationId, string channelName, UpsertCommunicationRequest request, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO communication_channel_content (communication_id, channel_id, subject, content)
                VALUES (
                    @CommunicationId, 
                    (SELECT channel_id FROM communication_channels WHERE name = @ChannelName), 
                    @Subject, 
                    @Content
                );";

            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@CommunicationId", communicationId);
                command.Parameters.AddWithValue("@ChannelName", channelName); // "Email" or "WhatsApp"
                command.Parameters.AddWithValue("@Subject", channelName == "Email" ? (object)request.Title : DBNull.Value);
                command.Parameters.AddWithValue("@Content", request.Content);
                
                int rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }

        // --- UPDATED to new schema (and uses your account_movements logic) ---
        public async Task<bool> InsertCommunicationRecipientsAsync(int communicationId, List<string> recipients, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                WITH AccountBalance AS (
                    SELECT 
                        r.client_id, 
                        SUM(CASE WHEN am.movement_type = 'DEBITO' THEN am.amount ELSE -am.amount END) AS Balance
                    FROM account_movements am
                    JOIN rentals r ON am.rental_id = r.rental_id
                    GROUP BY r.client_id
                )
                INSERT INTO communication_recipients (communication_id, client_id)
                SELECT DISTINCT @CommunicationId, cl.client_id
                FROM clients cl
                LEFT JOIN AccountBalance ab ON cl.client_id = ab.client_id
                WHERE 
                    cl.active = 1 AND (
                        (1 = @IncludeAll)
                        OR
                        (1 = @IncludeMorosos AND ISNULL(ab.Balance, 0) > 0)
                        OR
                        (1 = @IncludeAlDia AND ISNULL(ab.Balance, 0) <= 0)
                        OR
                        ((cl.first_name + ' ' + cl.last_name) IN (SELECT value FROM STRING_SPLIT(@ClientNames, ',')))
                        OR
                        (cl.first_name IN (SELECT value FROM STRING_SPLIT(@ClientNames, ',')))
                    );
            ";

            var individualNames = recipients.Where(r => 
                !r.Equals("Todos los clientes", StringComparison.OrdinalIgnoreCase) && 
                !r.Equals("Clientes morosos", StringComparison.OrdinalIgnoreCase) &&
                !r.Equals("Clientes con deuda", StringComparison.OrdinalIgnoreCase) &&
                !r.Equals("Clientes al día", StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@CommunicationId", communicationId);
                command.Parameters.AddWithValue("@IncludeAll", recipients.Contains("Todos los clientes") ? 1 : 0);
                command.Parameters.AddWithValue("@IncludeMorosos", recipients.Contains("Clientes morosos") ? 1 : 0);
                command.Parameters.AddWithValue("@IncludeAlDia", recipients.Contains("Clientes al día") ? 1 : 0);
                command.Parameters.AddWithValue("@ClientNames", string.Join(",", individualNames));

                await command.ExecuteNonQueryAsync();
                return true;
            }
        }

        #endregion

        #region Job Support Methods (Updated)

        // --- UPDATED to new schema ---
        public async Task UpdateCommunicationStatusAsync(int communicationId, string status)
        {
            string query = "UPDATE communications SET status = @Status WHERE communication_id = @Id";
            var parameters = new[]
            {
                new SqlParameter("@Status", status),
                new SqlParameter("@Id", communicationId)
            };
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        // --- UPDATED to new schema ---
        public async Task<List<ChannelForSendingDto>> GetChannelsForSendingAsync(int communicationId)
        {
            var channels = new List<ChannelForSendingDto>();
            string query = @"
                SELECT 
                    ccc.comm_channel_content_id AS IdCommChannelContent,
                    c.name AS ChannelName,
                    ccc.subject AS Subject,
                    ccc.content AS Content
                FROM communication_channel_content ccc
                JOIN communication_channels c ON ccc.channel_id = c.channel_id
                WHERE ccc.communication_id = @Id";
            
            var parameters = new[] { new SqlParameter("@Id", communicationId) };
            DataTable table = await _accessDB.GetTableAsync("Channels", query, parameters);

            foreach (DataRow row in table.Rows)
            {
                channels.Add(new ChannelForSendingDto
                {
                    CommChannelContentId = Convert.ToInt32(row["IdCommChannelContent"]),
                    ChannelName = row["ChannelName"].ToString() ?? "Unknown",
                    Subject = row["Subject"] is DBNull ? null : row["Subject"].ToString(),
                    Content = row["Content"].ToString() ?? ""
                });
            }
            return channels;
        }

        // --- UPDATED to new schema (using your tables) ---
        public async Task<List<RecipientForSendingDto>> GetRecipientsForSendingAsync(int communicationId)
        {
            var recipients = new List<RecipientForSendingDto>();
            string query = @"
                SELECT 
                    c.client_id AS Id,
                    c.first_name + ' ' + c.last_name AS Name,
                    (SELECT TOP 1 e.address 
                     FROM emails e 
                     WHERE e.client_id = c.client_id AND e.active = 1) AS Email,
                    (SELECT TOP 1 p.number 
                     FROM phones p 
                     WHERE p.client_id = c.client_id AND p.whatsapp = 1 AND p.active = 1) AS Phone
                FROM clients c
                JOIN communication_recipients cr ON c.client_id = cr.client_id
                WHERE cr.communication_id = @Id AND c.active = 1";
            
            var parameters = new[] { new SqlParameter("@Id", communicationId) };
            DataTable table = await _accessDB.GetTableAsync("Recipients", query, parameters);

            foreach (DataRow row in table.Rows)
            {
                recipients.Add(new RecipientForSendingDto
                {
                    ClientId = Convert.ToInt32(row["Id"]),
                    Name = row["Name"].ToString() ?? "",
                    Email = row["Email"] is DBNull ? null : row["Email"].ToString(),
                    Phone = row["Phone"] is DBNull ? null : row["Phone"].ToString()
                });
            }
            return recipients;
        }

        // --- UPDATED to new schema ---
        public async Task LogSendAttemptAsync(int idCommChannelContent, int idCliente, string status, string response)
        {
            string query = @"
                INSERT INTO dispatches (comm_channel_content_id, client_id, dispatch_date, status, provider_response)
                VALUES (@IdCommChannelContent, @IdCliente, GETDATE(), @Status, @Response)";

            if (response.Length > 500)
            {
                response = response.Substring(0, 500);
            }

            var parameters = new[]
            {
                new SqlParameter("@IdCommChannelContent", idCommChannelContent),
                new SqlParameter("@IdCliente", idCliente),
                new SqlParameter("@Status", status), // 'Successful' or 'Failed'
                new SqlParameter("@Response", response)
            };
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        #endregion

        // --- UPDATED MapDataRowToDto (Fixes GET error) ---
        private CommunicationDto MapDataRowToDto(DataRow row)
        {
            // Reads the comma-separated string
            var recipientsCsv = row["RecipientsCsv"].ToString() ?? "";

            return new CommunicationDto
            {
                Id = Convert.ToInt32(row["Id"]),
                Title = row["Title"]?.ToString() ?? "",
                Content = row["Content"] is DBNull ? "" : row["Content"].ToString(),
                SendDate = row["SendDate"] is DBNull ? null : row["SendDate"].ToString(),
                SendTime = row["SendTime"] is DBNull ? null : row["SendTime"].ToString(),
                Status = row["Status"]?.ToString() ?? "Draft",
                CreationDate = row["CreationDate"]?.ToString() ?? "",
                Channel = row["Channel"]?.ToString() ?? "",

                // Splits the string into a List<string>
                Recipients = string.IsNullOrEmpty(recipientsCsv)
                             ? new List<string>()
                             : recipientsCsv.Split(',').ToList()
            };
        }
        
        // This method is simple, it just deletes
        public async Task<bool> DeleteCommunicationAsync(int communicationId)
        {
            // Deleting the main communication should cascade delete all related content
            // if your database FOREIGN KEYs are set up with ON DELETE CASCADE.
            // If not, you must delete from child tables first.
            
            // Deleting child rows first to be safe
            string query = @"
                DELETE FROM dispatches WHERE comm_channel_content_id IN (SELECT comm_channel_content_id FROM communication_channel_content WHERE communication_id = @Id);
                DELETE FROM communication_recipients WHERE communication_id = @Id;
                DELETE FROM communication_channel_content WHERE communication_id = @Id;
                DELETE FROM communications WHERE communication_id = @Id;
            ";
            
            var parameters = new[] { new SqlParameter("@Id", communicationId) };
            int rows = await _accessDB.ExecuteCommandAsync(query, parameters);
            return rows > 0;
        }

        // This method will be used for 'send now'
        public async Task<bool> UpdateCommunicationStatusAndDateAsync(int communicationId, string status, DateTime scheduledDate)
        {
            string query = "UPDATE communications SET status = @Status, scheduled_date = @Date WHERE communication_id = @Id";
            var parameters = new[]
            {
                new SqlParameter("@Status", status),
                new SqlParameter("@Date", scheduledDate),
                new SqlParameter("@Id", communicationId)
            };
            int rows = await _accessDB.ExecuteCommandAsync(query, parameters);
            return rows > 0;
        }

        // Note: The main 'Update' (for edit) is complex because it uses a transaction
        // to delete old channels/recipients and add new ones.
        // We will add this logic in the *Service* layer.
    }
}