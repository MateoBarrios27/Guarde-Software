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

        #region 1. Consultas Principales (Grid y Detalles)

        private const string GET_COMMUNICATIONS_QUERY = @"
            SELECT 
                c.communication_id AS Id,
                c.title AS Title,
                (SELECT TOP 1 content FROM communication_channel_content WHERE communication_id = c.communication_id) AS Content,
                FORMAT(c.scheduled_date, 'yyyy-MM-dd') AS SendDate,
                FORMAT(c.scheduled_date, 'HH:mm') AS SendTime,
                c.status AS Status,
                FORMAT(c.creation_date, 'yyyy-MM-dd') AS CreationDate,
                
                -- Nombre de la configuración SMTP usada (si existe)
                (SELECT name FROM smtp_configurations WHERE smtp_id = c.smtp_configuration_id) AS SmtpName,
                c.smtp_configuration_id AS SmtpConfigId,

                (SELECT STRING_AGG(chan.name, ' + ') 
                 FROM communication_channel_content ccc
                 JOIN communication_channels chan ON ccc.channel_id = chan.channel_id
                 WHERE ccc.communication_id = c.communication_id) AS Channel,
                
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

        private CommunicationDto MapDataRowToDto(DataRow row)
        {
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
                SmtpConfigId = row["SmtpConfigId"] is DBNull ? null : (int?)row["SmtpConfigId"],
                Recipients = string.IsNullOrEmpty(recipientsCsv)
                             ? new List<string>()
                             : recipientsCsv.Split(',').ToList()
            };
        }

        #endregion

        #region 2. Métodos Transaccionales (Insert / Update)

        public async Task<int> InsertCommunicationAsync(UpsertCommunicationRequest request, int userId, DateTime? scheduledAt, string status, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO communications (creator_user_id, title, creation_date, scheduled_date, status, smtp_configuration_id)
                OUTPUT INSERTED.communication_id
                VALUES (@CreatorUserId, @Title, GETDATE(), @ScheduledDate, @Status, @SmtpConfigId);";
            
            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@CreatorUserId", userId);
                command.Parameters.AddWithValue("@Title", request.Title);
                command.Parameters.AddWithValue("@ScheduledDate", (object)scheduledAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@Status", status); 
                command.Parameters.AddWithValue("@SmtpConfigId", (object)request.SmtpConfigId ?? DBNull.Value);

                object result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

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
                command.Parameters.AddWithValue("@ChannelName", channelName); 
                command.Parameters.AddWithValue("@Subject", channelName == "Email" ? (object)request.Title : DBNull.Value);
                command.Parameters.AddWithValue("@Content", request.Content);
                
                int rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }

        // Inserta Metadatos de Archivos Adjuntos
        public async Task InsertAttachmentsAsync(int communicationId, List<AttachmentDto> attachments, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO communication_attachments (communication_id, file_name, file_path, content_type) 
                VALUES (@Id, @Name, @Path, @Type)";
            
            foreach(var att in attachments) 
            {
                using(var cmd = new SqlCommand(query, connection, transaction)) 
                {
                    cmd.Parameters.AddWithValue("@Id", communicationId);
                    cmd.Parameters.AddWithValue("@Name", att.FileName);
                    cmd.Parameters.AddWithValue("@Path", att.FilePath);
                    cmd.Parameters.AddWithValue("@Type", att.ContentType ?? "");
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<bool> InsertCommunicationRecipientsAsync(int communicationId, List<string> recipients, SqlConnection connection, SqlTransaction transaction)
        {
            // Lógica para desglosar grupos (Todos, Morosos, etc) y nombres individuales
            // Usando tu lógica de AccountBalance
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

        public async Task<bool> DeleteCommunicationAsync(int communicationId)
        {
            // Borrado en cascada manual por seguridad
            string query = @"
                DELETE FROM dispatches WHERE comm_channel_content_id IN (SELECT comm_channel_content_id FROM communication_channel_content WHERE communication_id = @Id);
                DELETE FROM communication_recipients WHERE communication_id = @Id;
                DELETE FROM communication_attachments WHERE communication_id = @Id; -- Borra adjuntos
                DELETE FROM communication_channel_content WHERE communication_id = @Id;
                DELETE FROM communications WHERE communication_id = @Id;
            ";
            
            var parameters = new[] { new SqlParameter("@Id", communicationId) };
            int rows = await _accessDB.ExecuteCommandAsync(query, parameters);
            return rows > 0;
        }

        #endregion

        #region 3. Métodos de Soporte para el JOB (Envío)

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
        
        // Usado para "Enviar Ahora" y "Reintentar"
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

        // Obtener Canales y contenido
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

        // Obtener Destinatarios (CON LOGICA DE REINTENTO)
        public async Task<List<RecipientForSendingDto>> GetRecipientsForSendingAsync(int communicationId)
        {
            var recipients = new List<RecipientForSendingDto>();
            
            // Esta query excluye a los clientes que ya tienen un registro 'Exitoso' en la tabla dispatches
            // para esta comunicación. Esto permite que el Job funcione como "Enviar Todo" y "Reintentar Fallidos".
            string query = @"
                SELECT DISTINCT
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
                WHERE cr.communication_id = @Id AND c.active = 1
                AND c.client_id NOT IN (
                    SELECT client_id FROM dispatches 
                    WHERE comm_channel_content_id IN (
                        SELECT comm_channel_content_id 
                        FROM communication_channel_content 
                        WHERE communication_id = @Id
                    )
                    AND status = 'Exitoso'
                )";
            
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

        // Obtener Configuración SMTP Específica (incluye datos de BCC)
        public async Task<SmtpSettingsModel?> GetSmtpSettingsAsync(int communicationId)
        {
            string query = @"
                SELECT s.host, s.port, s.email, s.password, s.use_ssl, s.enable_bcc, s.bcc_email
                FROM smtp_configurations s
                JOIN communications c ON c.smtp_configuration_id = s.smtp_id
                WHERE c.communication_id = @Id AND s.is_active = 1";
            
            var dt = await _accessDB.GetTableAsync("Smtp", query, new[] { new SqlParameter("@Id", communicationId) });
            
            if (dt.Rows.Count > 0) {
                var row = dt.Rows[0];
                return new SmtpSettingsModel {
                    Host = row["host"].ToString(),
                    Port = Convert.ToInt32(row["port"]),
                    Email = row["email"].ToString(),
                    Password = row["password"].ToString(),
                    UseSsl = Convert.ToBoolean(row["use_ssl"]),
                    EnableBcc = row["enable_bcc"] != DBNull.Value && Convert.ToBoolean(row["enable_bcc"]),
                    BccEmail = row["bcc_email"]?.ToString() ?? ""
                };
            }
            return null;
        }

        // Obtener Archivos Adjuntos para el Job
        public async Task<List<AttachmentDto>> GetAttachmentsAsync(int communicationId)
        {
            string query = "SELECT file_name, file_path, content_type FROM communication_attachments WHERE communication_id = @Id";
            var dt = await _accessDB.GetTableAsync("Att", query, new[] { new SqlParameter("@Id", communicationId) });
            var list = new List<AttachmentDto>();
            foreach(DataRow row in dt.Rows) {
                list.Add(new AttachmentDto {
                    FileName = row["file_name"].ToString(),
                    FilePath = row["file_path"].ToString(),
                    ContentType = row["content_type"].ToString()
                });
            }
            return list;
        }

        // Loggear intento de envío
        public async Task LogSendAttemptAsync(int idCommChannelContent, int idCliente, string status, string response)
        {
            string query = @"
                INSERT INTO dispatches (comm_channel_content_id, client_id, dispatch_date, status, provider_response)
                VALUES (@IdCommChannelContent, @IdCliente, GETDATE(), @Status, @Response)";

            if (!string.IsNullOrEmpty(response) && response.Length > 500)
            {
                response = response.Substring(0, 500);
            }

            var parameters = new[]
            {
                new SqlParameter("@IdCommChannelContent", idCommChannelContent),
                new SqlParameter("@IdCliente", idCliente),
                new SqlParameter("@Status", status), 
                new SqlParameter("@Response", response ?? "")
            };
            await _accessDB.ExecuteCommandAsync(query, parameters);
        }

        #endregion

        #region 4. Historial Cliente (Client Detail)

        public async Task<List<ClientCommunicationDto>> GetCommunicationsByClientIdAsync(int clientId)
        {
            var communications = new List<ClientCommunicationDto>();
            string query = @"
                SELECT 
                    d.dispatch_id AS Id,
                    d.dispatch_date AS Date,
                    LOWER(ch.name) AS Type, 
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
                communications.Add(new ClientCommunicationDto
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Date = Convert.ToDateTime(row["Date"]),
                    Type = row["Type"]?.ToString() ?? "system",
                    Subject = row["Subject"] is DBNull ? "" : row["Subject"].ToString(),
                    Snippet = row["Snippet"] is DBNull ? "" : row["Snippet"].ToString()
                });
            }
            return communications;
        }

        #endregion

        #region 5. ABML de Configuraciones SMTP (Settings)

        public async Task<List<SmtpConfigurationDto>> GetAllSmtpConfigsAsync()
        {
            var list = new List<SmtpConfigurationDto>();
            string query = "SELECT * FROM smtp_configurations WHERE is_active = 1";
            var dt = await _accessDB.GetTableAsync("SmtpList", query);
            
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new SmtpConfigurationDto
                {
                    Id = Convert.ToInt32(row["smtp_id"]),
                    Name = row["name"].ToString(),
                    Host = row["host"].ToString(),
                    Port = Convert.ToInt32(row["port"]),
                    Email = row["email"].ToString(),
                    Password = row["password"].ToString(),
                    UseSsl = Convert.ToBoolean(row["use_ssl"]),
                    EnableBcc = row["enable_bcc"] != DBNull.Value && Convert.ToBoolean(row["enable_bcc"]),
                    BccEmail = row["bcc_email"]?.ToString() ?? ""
                });
            }
            return list;
        }

        public async Task<int> CreateSmtpConfigAsync(SmtpConfigurationDto dto)
        {
            string query = @"
                INSERT INTO smtp_configurations (name, host, port, email, password, use_ssl, enable_bcc, bcc_email, is_active)
                OUTPUT INSERTED.smtp_id
                VALUES (@Name, @Host, @Port, @Email, @Password, @UseSsl, @EnableBcc, @BccEmail, 1)";
            
            var param = new[] {
                new SqlParameter("@Name", dto.Name),
                new SqlParameter("@Host", dto.Host),
                new SqlParameter("@Port", dto.Port),
                new SqlParameter("@Email", dto.Email),
                new SqlParameter("@Password", dto.Password),
                new SqlParameter("@UseSsl", dto.UseSsl),
                new SqlParameter("@EnableBcc", dto.EnableBcc),
                new SqlParameter("@BccEmail", string.IsNullOrEmpty(dto.BccEmail) ? DBNull.Value : dto.BccEmail)
            };

            return (int)await _accessDB.ExecuteScalarAsync(query, param);
        }

        public async Task UpdateSmtpConfigAsync(SmtpConfigurationDto dto)
        {
            string query = @"
                UPDATE smtp_configurations 
                SET name=@Name, host=@Host, port=@Port, email=@Email, password=@Password, 
                    use_ssl=@UseSsl, enable_bcc=@EnableBcc, bcc_email=@BccEmail
                WHERE smtp_id=@Id";
            
            var param = new[] {
                new SqlParameter("@Id", dto.Id),
                new SqlParameter("@Name", dto.Name),
                new SqlParameter("@Host", dto.Host),
                new SqlParameter("@Port", dto.Port),
                new SqlParameter("@Email", dto.Email),
                new SqlParameter("@Password", dto.Password),
                new SqlParameter("@UseSsl", dto.UseSsl),
                new SqlParameter("@EnableBcc", dto.EnableBcc),
                new SqlParameter("@BccEmail", (object)dto.BccEmail ?? DBNull.Value)
            };
            await _accessDB.ExecuteCommandAsync(query, param);
        }

        public async Task DeleteSmtpConfigAsync(int id)
        {
            // Soft delete para no romper integridad referencial con historial
            string query = "UPDATE smtp_configurations SET is_active = 0 WHERE smtp_id = @Id";
            await _accessDB.ExecuteCommandAsync(query, [new SqlParameter("@Id", id)]);
        }

        #endregion
    }
}