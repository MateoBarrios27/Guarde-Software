using GuardeSoftwareAPI.Dtos.Communication;
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

        // --- CONSULTA ACTUALIZADA ---
        // Ahora incluye el campo 'attachments' (como JSON)
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
                
                -- Obtiene el JSON de adjuntos (asumiendo que solo está en el canal de Email)
                (SELECT TOP 1 ccc.attachments 
                 FROM communication_channel_content ccc 
                 WHERE ccc.communication_id = c.communication_id AND ccc.channel_id = 1) AS AttachmentsJson,

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
                command.Parameters.AddWithValue("@Status", status); 

                object result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        // --- MÉTODO ACTUALIZADO ---
        // Ahora acepta el JSON de adjuntos
        public async Task<bool> InsertCommunicationChannelAsync(
            int communicationId, 
            string channelName, 
            UpsertCommunicationRequest request, 
            string attachmentsJson, // JSON serializado de List<AttachmentDto>
            SqlConnection connection, 
            SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO communication_channel_content (communication_id, channel_id, subject, content, attachments)
                VALUES (
                    @CommunicationId, 
                    (SELECT channel_id FROM communication_channels WHERE name = @ChannelName), 
                    @Subject, 
                    @Content,
                    @AttachmentsJson
                );";

            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@CommunicationId", communicationId);
                command.Parameters.AddWithValue("@ChannelName", channelName);
                command.Parameters.AddWithValue("@Subject", channelName == "Email" ? (object)request.Title : DBNull.Value);
                command.Parameters.AddWithValue("@Content", request.Content);
                // Añade el JSON. Si no es Email, 'attachmentsJson' será "[]"
                command.Parameters.AddWithValue("@AttachmentsJson", attachmentsJson); 
                
                int rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }
        
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

        // --- NUEVO: Métodos para 'Update' transaccional ---
        public async Task<bool> DeleteCommunicationChildrenAsync(int communicationId, SqlConnection connection, SqlTransaction transaction)
        {
            string deleteQuery = @"
                DELETE FROM dispatches WHERE comm_channel_content_id IN (SELECT comm_channel_content_id FROM communication_channel_content WHERE communication_id = @Id);
                DELETE FROM communication_recipients WHERE communication_id = @Id;
                DELETE FROM communication_channel_content WHERE communication_id = @Id;
            ";
            using (var cmdDelete = new SqlCommand(deleteQuery, connection, transaction))
            {
                cmdDelete.Parameters.AddWithValue("@Id", communicationId);
                await cmdDelete.ExecuteNonQueryAsync();
                return true;
            }
        }
        
        public async Task<bool> UpdateCommunicationMainAsync(int communicationId, UpsertCommunicationRequest request, DateTime? scheduledAt, string status, SqlConnection connection, SqlTransaction transaction)
        {
            string updateQuery = @"
                UPDATE communications 
                SET title = @Title, scheduled_date = @ScheduledDate, status = @Status
                WHERE communication_id = @Id";
            
            using (var cmdUpdate = new SqlCommand(updateQuery, connection, transaction))
            {
                cmdUpdate.Parameters.AddWithValue("@Id", communicationId);
                cmdUpdate.Parameters.AddWithValue("@Title", request.Title);
                cmdUpdate.Parameters.AddWithValue("@ScheduledDate", (object)scheduledAt ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("@Status", status);
                await cmdUpdate.ExecuteNonQueryAsync();
                return true;
            }
        }

        #endregion

        #region Job Support Methods (Updated)

        public async Task UpdateCommunicationStatusAsync(int communicationId, string status)
        {
            string query = "UPDATE communications SET status = @Status WHERE communication_id = @Id";
            var parameters = new[] { new SqlParameter("@Status", status), new SqlParameter("@Id", communicationId) };
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        // --- MÉTODO ACTUALIZADO ---
        // Ahora también obtiene el JSON de adjuntos
        public async Task<List<ChannelForSendingDto>> GetChannelsForSendingAsync(int communicationId)
        {
            var channels = new List<ChannelForSendingDto>();
            string query = @"
                SELECT 
                    ccc.comm_channel_content_id AS IdCommChannelContent,
                    c.name AS ChannelName,
                    ccc.subject AS Subject,
                    ccc.content AS Content,
                    ccc.attachments AS AttachmentsJson
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
                    Content = row["Content"].ToString() ?? "",
                    AttachmentsJson = row["AttachmentsJson"] is DBNull ? null : row["AttachmentsJson"].ToString()
                });
            }
            return channels;
        }

        // --- MÉTODO ACTUALIZADO ---
        // Añadida lógica para 'retryMode' (Reintentar solo fallidos)
        public async Task<List<RecipientForSendingDto>> GetRecipientsForSendingAsync(int communicationId, bool isRetry)
        {
            var recipients = new List<RecipientForSendingDto>();
            string query = @"
                SELECT 
                    c.client_id AS Id,
                    c.first_name + ' ' + c.last_name AS Name,
                    (SELECT TOP 1 e.address FROM emails e WHERE e.client_id = c.client_id AND e.active = 1) AS Email,
                    (SELECT TOP 1 p.number FROM phones p WHERE p.client_id = c.client_id AND p.whatsapp = 1 AND p.active = 1) AS Phone
                FROM clients c
                JOIN communication_recipients cr ON c.client_id = cr.client_id
                WHERE cr.communication_id = @Id AND c.active = 1";

            // Si es un reintento, solo trae clientes que fallaron o quedaron pendientes
            if (isRetry)
            {
                query += @" 
                    AND c.client_id IN (
                        SELECT d.client_id 
                        FROM dispatches d
                        JOIN communication_channel_content ccc ON d.comm_channel_content_id = ccc.comm_channel_content_id
                        WHERE ccc.communication_id = @Id
                        AND d.status IN ('Fallido', 'Pendiente')
                    )";
            }
            
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

        public async Task LogSendAttemptAsync(int idCommChannelContent, int idCliente, string status, string response)
        {
            // Este query es un 'UPSERT'. Inserta si no existe, o actualiza si ya existe (para reintentos).
            string query = @"
                IF EXISTS (SELECT 1 FROM dispatches WHERE comm_channel_content_id = @IdCommChannelContent AND client_id = @IdCliente)
                BEGIN
                    UPDATE dispatches 
                    SET dispatch_date = GETDATE(), status = @Status, provider_response = @Response
                    WHERE comm_channel_content_id = @IdCommChannelContent AND client_id = @IdCliente
                END
                ELSE
                BEGIN
                    INSERT INTO dispatches (comm_channel_content_id, client_id, dispatch_date, status, provider_response)
                    VALUES (@IdCommChannelContent, @IdCliente, GETDATE(), @Status, @Response)
                END";

            if (response.Length > 500) response = response.Substring(0, 500);

            var parameters = new[]
            {
                new SqlParameter("@IdCommChannelContent", idCommChannelContent),
                new SqlParameter("@IdCliente", idCliente),
                new SqlParameter("@Status", status), // 'Exitoso' or 'Fallido'
                new SqlParameter("@Response", response)
            };
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        #endregion

        // --- MÉTODO ACTUALIZADO ---
        private CommunicationDto MapDataRowToDto(DataRow row)
        {
            var recipientsCsv = row["RecipientsCsv"].ToString() ?? "";
            var attachmentsJson = row["AttachmentsJson"] is DBNull ? "[]" : row["AttachmentsJson"].ToString();

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
                Recipients = string.IsNullOrEmpty(recipientsCsv)
                             ? new List<string>()
                             : recipientsCsv.Split(',').ToList(),
                // Deserializa el JSON de adjuntos
                Attachments = JsonSerializer.Deserialize<List<AttachmentDto>>(attachmentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AttachmentDto>()
            };
        }
        
        public async Task<bool> DeleteCommunicationAsync(int communicationId)
        {
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

        public async Task<List<ClientCommunicationDto>> GetCommunicationsByClientIdAsync(int clientId)
        {
            var communications = new List<ClientCommunicationDto>();
            string query = @"
                SELECT 
                    d.dispatch_id AS Id,
                    d.dispatch_date AS Date,
                    LOWER(ch.name) AS Type, -- 'email', 'whatsapp'
                    ccc.subject AS Subject,
                    CASE 
                        WHEN LEN(ccc.content) > 150 THEN LEFT(ccc.content, 150) + '...'
                        ELSE ccc.content
                    END AS Snippet
                FROM dispatches d
                JOIN communication_channel_content ccc ON d.comm_channel_content_id = ccc.comm_channel_content_id
                JOIN communication_channels ch ON ccc.channel_id = ch.channel_id
                WHERE d.client_id = @ClientId
                  AND d.status = 'Exitoso'
                ORDER BY d.dispatch_date DESC;";

            var parameters = new[] { new SqlParameter("@ClientId", clientId) };
            DataTable table = await _accessDB.GetTableAsync("ClientCommunications", query, parameters);

            foreach (DataRow row in table.Rows)
            {
                communications.Add(MapDataRowToClientCommunicationDto(row));
            }
            return communications;
        }

        private ClientCommunicationDto MapDataRowToClientCommunicationDto(DataRow row)
        {
            return new ClientCommunicationDto
            {
                Id = Convert.ToInt32(row["Id"]),
                Date = Convert.ToDateTime(row["Date"]),
                Type = row["Type"]?.ToString() ?? "system",
                Subject = row["Subject"] is DBNull ? "" : row["Subject"].ToString(),
                Snippet = row["Snippet"] is DBNull ? "" : row["Snippet"].ToString()
            };
        }

        // --- NUEVOS MÉTODOS DAO para Adjuntos ---

        public async Task<List<AttachmentDto>> GetAttachmentsAsync(int communicationId)
        {
            string query = @"
                SELECT TOP 1 attachments 
                FROM communication_channel_content 
                WHERE communication_id = @Id AND channel_id = 1"; // Asume Email = 1
            
            var parameters = new[] { new SqlParameter("@Id", communicationId) };
            var jsonResult = await _accessDB.ExecuteScalarAsync(query, parameters);
            
            if (jsonResult != null && jsonResult != DBNull.Value)
            {
                return JsonSerializer.Deserialize<List<AttachmentDto>>(jsonResult.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AttachmentDto>();
            }
            return new List<AttachmentDto>();
        }

        public async Task<bool> RemoveAttachmentFromJsonAsync(int communicationId, string fileName)
        {
            // Esta lógica es compleja en SQL. Requiere leer el JSON, modificarlo y reescribirlo.
            // Es más fácil hacerlo en la lógica de servicio (en UpdateCommunicationAsync)
            // Este método podría simplemente setear el JSON.
            // Por ahora, asumimos que 'UpdateCommunicationAsync' maneja esto.
            return await Task.FromResult(true); // Placeholder
        }
    }
}