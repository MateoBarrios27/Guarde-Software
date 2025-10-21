using GuardeSoftwareAPI.Dao; // Your AccessDB
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Dao
{
    public class CommunicationDao
    {
        private readonly AccessDB _accessDB;

        public CommunicationDao(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        // This query "translates" your normalized tables into the flat DTO
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
                ISNULL(
                    (SELECT cl.nombre 
                     FROM communication_recipients cr
                     JOIN clients cl ON cr.client_id = cl.client_id
                     WHERE cr.communication_id = c.communication_id
                     FOR JSON PATH), 
                '[]') AS RecipientsJson
            FROM 
                communications c
        ";
        
        // This method uses your AccessDB helper, as it doesn't need a transaction
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

        // This method also uses your AccessDB helper
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

        #region Transactional Methods

        // These methods follow your DaoAccountMovement pattern.
        // They do NOT use AccessDB helpers, as they must join an existing transaction.

        /// <summary>
        /// (Step 1) Inserts the main communication record and returns the new ID.
        /// </summary>
        public async Task<int> InsertCommunicationAsync(UpsertCommunicationRequest request, int userId, DateTime? scheduledAt, string status, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO communications (creator_user_id, title, creation_date, scheduled_date, status)
                OUTPUT INSERTED.communication_id
                VALUES (@UserId, @Title, GETDATE(), @ScheduledDate, @Status);";
            
            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@Title", request.Title);
                command.Parameters.AddWithValue("@ScheduledDate", (object)scheduledAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@Status", status);

                object result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// (Step 2) Inserts a related channel-specific record.
        /// </summary>
        public async Task<bool> InsertCommunicationChannelContentAsync(int communicationId, string channelName, UpsertCommunicationRequest request, SqlConnection connection, SqlTransaction transaction)
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
        
        /// <summary>
        /// (Step 3) Inserts all recipients by resolving groups and individual names.
        /// THIS IS THE CORRECTED AND TRANSLATED VERSION.
        /// </summary>
        public async Task<bool> InsertCommunicationRecipientsAsync(int communicationId, List<string> recipients, SqlConnection connection, SqlTransaction transaction)
        {
            // This CTE calculates the real-time balance for each client from your schema
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
                        -- Group 1: 'All Clients'
                        (1 = @IncludeAll)
                        OR
                        -- Group 2: 'Overdue Clients' (Balance > 0)
                        (1 = @IncludeOverdue AND ISNULL(ab.Balance, 0) > 0)
                        OR
                        -- Group 3: 'Up-to-Date Clients' (Balance <= 0)
                        (1 = @IncludeUpToDate AND ISNULL(ab.Balance, 0) <= 0)
                        OR
                        -- Individual Clients by full name (e.g., 'Bruno' or 'Juan Pérez')
                        ( (cl.first_name + ' ' + cl.last_name) IN (SELECT value FROM STRING_SPLIT(@ClientNames, ',')) )
                        OR
                        ( cl.first_name IN (SELECT value FROM STRING_SPLIT(@ClientNames, ',')) )
                    );
            ";

            // Filter out group names (now in English) to create a list of only individual names
            var individualNames = recipients.Where(r => 
                !r.Equals("All Clients", StringComparison.OrdinalIgnoreCase) && 
                !r.Equals("Overdue Clients", StringComparison.OrdinalIgnoreCase) &&
                !r.Equals("Clients with Debt", StringComparison.OrdinalIgnoreCase) && // Adding this just in case
                !r.Equals("Up-to-Date Clients", StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@CommunicationId", communicationId);
                command.Parameters.AddWithValue("@IncludeAll", recipients.Contains("All Clients") ? 1 : 0);
                command.Parameters.AddWithValue("@IncludeOverdue", recipients.Contains("Overdue Clients") ? 1 : 0);
                command.Parameters.AddWithValue("@IncludeUpToDate", recipients.Contains("Up-to-Date Clients") ? 1 : 0);
                
                // Pass the comma-separated list of individual names
                command.Parameters.AddWithValue("@ClientNames", string.Join(",", individualNames));

                await command.ExecuteNonQueryAsync();
                return true;
            }
        }

        #endregion

        /// <summary>
        /// Private helper to map a DataRow to our DTO.
        /// (This method remains unchanged as it maps from aliases)
        /// </summary>
        private CommunicationDto MapDataRowToDto(DataRow row)
        {
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
                Recipients = JsonSerializer.Deserialize<List<string>>(row["RecipientsJson"].ToString()!)
                                 ?? new List<string>()
            };
        }
        
        /// <summary>
        /// Updates the master status of a 'communication'.
        /// (e.g., 'Processing', 'Finished')
        /// </summary>
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

        /// <summary>
        /// Gets the list of channels and their content for a specific 'communication'.
        /// </summary>
        public async Task<List<ChannelForSendingDto>> GetChannelsForSendingAsync(int communicationId)
        {
            var channels = new List<ChannelForSendingDto>();
            string query = @"
                SELECT 
                    ccc.comm_channel_content_id AS Id,
                    chan.name AS ChannelName,
                    ccc.subject AS Subject,
                    ccc.content AS Content
                FROM communication_channel_content ccc
                JOIN communication_channels chan ON ccc.channel_id = chan.channel_id
                WHERE ccc.communication_id = @Id";
            
            var parameters = new[] { new SqlParameter("@Id", communicationId) };
            DataTable table = await _accessDB.GetTableAsync("Channels", query, parameters);

            foreach (DataRow row in table.Rows)
            {
                channels.Add(new ChannelForSendingDto
                {
                    // Assumes you rename this property in your DTO as well
                    CommChannelContentId = Convert.ToInt32(row["Id"]), 
                    ChannelName = row["ChannelName"].ToString() ?? "Unknown",
                    Subject = row["Subject"] is DBNull ? null : row["Subject"].ToString(),
                    Content = row["Content"].ToString() ?? ""
                });
            }
            return channels;
        }

        /// <summary>
        /// Gets the list of all clients (and their contact info) for a 'communication'.
        /// THIS IS THE CORRECTED VERSION.
        /// </summary>
        public async Task<List<RecipientForSendingDto>> GetRecipientsForSendingAsync(int communicationId)
        {
            var recipients = new List<RecipientForSendingDto>();
            // This query now joins 'emails' and 'phones' tables correctly
            string query = @"
                SELECT 
                    c.client_id AS Id,
                    c.first_name + ' ' + c.last_name AS Name,
                    
                    -- Get the first active email for the client
                    (SELECT TOP 1 e.address 
                     FROM emails e 
                     WHERE e.client_id = c.client_id AND e.active = 1) AS Email,
                    
                    -- Get the first active WhatsApp number for the client
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

        /// <summary>
        /// Logs every single send attempt to the 'dispatches' table.
        /// (e.g., 'Successful', 'Failed')
        /// </summary>
        public async Task LogSendAttemptAsync(int commChannelContentId, int clientId, string status, string response)
        {
            string query = @"
                INSERT INTO dispatches (comm_channel_content_id, client_id, dispatch_date, status, provider_response)
                VALUES (@CommChannelContentId, @ClientId, GETDATE(), @Status, @ProviderResponse)";

            // Truncate response to fit the NVARCHAR(500) column
            if (response.Length > 500)
            {
                response = response.Substring(0, 500);
            }

            var parameters = new[]
            {
                new SqlParameter("@CommChannelContentId", commChannelContentId),
                new SqlParameter("@ClientId", clientId),
                new SqlParameter("@Status", status),
                new SqlParameter("@ProviderResponse", response)
            };
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }
    }
}