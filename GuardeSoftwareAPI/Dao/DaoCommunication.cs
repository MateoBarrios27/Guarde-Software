using GuardeSoftwareAPI.Dtos.Communication;
using GuardeSoftwareAPI.Dao;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using GuardeSoftwareAPI.Dtos.Client;

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
                c.is_account_statement,
                c.is_next_month_statement,

                (SELECT STRING_AGG(chan.name, ' + ') 
                 FROM communication_channel_content ccc
                 JOIN communication_channels chan ON ccc.channel_id = chan.channel_id
                 WHERE ccc.communication_id = c.communication_id) AS Channel,
                
                ISNULL(
                    (SELECT STRING_AGG(cl.full_name, ',')
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
                             : recipientsCsv.Split(',').ToList(),
                IsAccountStatement = row["is_account_statement"] is not DBNull && Convert.ToBoolean(row["is_account_statement"]),
                IsNextMonthStatement = row["is_next_month_statement"] is not DBNull && Convert.ToBoolean(row["is_next_month_statement"])
            };
        }

        #endregion

        #region 2. Métodos Transaccionales (Insert / Update)

        public async Task<int> InsertCommunicationAsync(UpsertCommunicationRequest request, int userId, DateTime? scheduledAt, string status, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO communications (creator_user_id, title, creation_date, scheduled_date, status, smtp_configuration_id, is_account_statement, is_next_month_statement)
                OUTPUT INSERTED.communication_id
                VALUES (@CreatorUserId, @Title, GETDATE(), @ScheduledDate, @Status, @SmtpConfigId, @IsAccountStatement, @IsNextMonthStatement);";

            using (SqlCommand command = new(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@CreatorUserId", userId);
                command.Parameters.AddWithValue("@Title", request.Title);
                command.Parameters.AddWithValue("@ScheduledDate", (object)scheduledAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@Status", status); 
                command.Parameters.AddWithValue("@SmtpConfigId", (object)request.SmtpConfigId ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsAccountStatement", request.IsAccountStatement);
                command.Parameters.AddWithValue("@IsNextMonthStatement", request.IsNextMonthStatement);

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

            using (SqlCommand command = new(query, connection, transaction))
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
            if (recipients == null || recipients.Count == 0) return false;

            string query = @"
                INSERT INTO communication_recipients (communication_id, client_id)
                SELECT DISTINCT @CommunicationId, c.client_id
                FROM clients c
                WHERE c.active = 1 
                AND c.full_name IN (
                    SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@ClientNames, ',')
                )";

            string joinedNames = string.Join(",", recipients);

            using (SqlCommand command = new(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@CommunicationId", communicationId);
                command.Parameters.AddWithValue("@ClientNames", joinedNames);
                await command.ExecuteNonQueryAsync();
            }
            return true;
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
                    c.full_name AS Name,
                    ISNULL((SELECT STRING_AGG(e.address, ';') 
                    FROM emails e 
                    WHERE e.client_id = c.client_id AND e.active = 1), '') AS Email,
                    (SELECT TOP 1 p.number 
                     FROM phones p 
                     WHERE p.client_id = c.client_id AND p.whatsapp = 1 AND p.active = 1) AS Phone
                FROM clients c
                JOIN communication_recipients cr ON c.client_id = cr.client_id
                WHERE cr.communication_id = @Id AND c.active = 1 AND c.receive_communications = 1
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
        public async Task<List<ClientRecipientDto>> GetClientsForSelectorAsync()
        {
            var list = new List<ClientRecipientDto>();
            
            string query = @"
                SELECT 
                    c.client_id AS Id,
                    c.full_name AS FullName,
                    (SELECT TOP 1 address FROM emails WHERE client_id = c.client_id AND active = 1) AS Email,
                    
                    ISNULL((
                        SELECT SUM(CASE WHEN am.movement_type = 'DEBITO' THEN -am.amount ELSE am.amount END)
                        FROM account_movements am
                        JOIN rentals r_mov ON am.rental_id = r_mov.rental_id
                        WHERE r_mov.client_id = c.client_id
                    ), 0) AS Balance,

                    ISNULL((
                        SELECT MAX(months_unpaid) 
                        FROM rentals 
                        WHERE client_id = c.client_id AND active = 1
                    ), 0) AS MaxUnpaidMonths,

                    ISNULL((
                        SELECT SUM(h.amount) 
                        FROM rentals r2 
                        JOIN rental_amount_history h ON r2.rental_id = h.rental_id AND h.end_date IS NULL 
                        WHERE r2.client_id = c.client_id AND r2.active = 1
                    ), 0) AS CurrentRentAmount,

                    -- MOTOR DE CÁLCULO DINÁMICO DE FECHA DE PAGO (INTEGRACIÓN)
                    CASE 
                        WHEN latest_cmb.MonthYearDB IS NOT NULL AND LEN(latest_cmb.MonthYearDB) = 7 THEN
                            CASE 
                                WHEN latest_cmb.NetBalance <= 0 THEN 
                                    DATEADD(month, 1, DATEFROMPARTS(CAST(RIGHT(latest_cmb.MonthYearDB, 4) AS INT), CAST(LEFT(latest_cmb.MonthYearDB, 2) AS INT), 1))
                                ELSE
                                    DATEFROMPARTS(CAST(RIGHT(ISNULL(db.MonthYearDB, latest_cmb.MonthYearDB), 4) AS INT), CAST(LEFT(ISNULL(db.MonthYearDB, latest_cmb.MonthYearDB), 2) AS INT), 1)
                            END
                        ELSE NULL 
                    END AS NextPaymentDate

                FROM clients c
                -- Vinculamos el alquiler activo principal para alimentar las relaciones temporales
                OUTER APPLY (
                    SELECT TOP 1 rental_id 
                    FROM rentals 
                    WHERE client_id = c.client_id AND active = 1
                ) r
                -- 1. BÚSQUEDA DEL ÚLTIMO MES ABSOLUTO (Para saber hasta dónde pagó o adelantó)
                OUTER APPLY (
                    SELECT TOP 1
                        MonthYearDB = cmb.month_year,
                        NetBalance = cmb.balance - cmb.paid - cmb.advanced_payment
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                    ORDER BY cmb.id DESC
                ) latest_cmb
                -- 2. BÚSQUEDA DEL MES ACTIVO (El más antiguo con deuda pendiente)
                OUTER APPLY (
                    SELECT TOP 1 MonthYearDB = cmb.month_year
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                      AND (cmb.balance - cmb.paid - cmb.advanced_payment) > 0
                    ORDER BY cmb.id DESC
                ) db
                WHERE c.active = 1
                ORDER BY c.full_name ASC";

            var dt = await _accessDB.GetTableAsync("ClientsSelector", query);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new ClientRecipientDto
                {
                    Id = Convert.ToInt32(row["Id"]),
                    FullName = row["FullName"].ToString() ?? "",
                    Email = row["Email"]?.ToString() ?? "",
                    Balance = Convert.ToDecimal(row["Balance"]),
                    MaxUnpaidMonths = Convert.ToInt32(row["MaxUnpaidMonths"]),
                    CurrentRentAmount = Convert.ToDecimal(row["CurrentRentAmount"]),
                    NextPaymentDate = row["NextPaymentDate"] != DBNull.Value ? Convert.ToDateTime(row["NextPaymentDate"]) : null
                });
            }
            return list;
        }

        public async Task<ClientFinancialDto> GetClientFinancialData(int clientId, bool isNextMonth = false)
        {
            string query = @"
                WITH CurrentRentalAmount AS (
                    SELECT h.rental_id, h.amount AS CurrentRent
                    FROM (
                        SELECT rental_id, amount,
                               ROW_NUMBER() OVER (PARTITION BY rental_id ORDER BY start_date DESC, CASE WHEN end_date IS NULL THEN 1 ELSE 0 END DESC, rental_amount_history_id DESC) as rn
                        FROM rental_amount_history WHERE start_date <= DATEADD(hour, -3, GETUTCDATE())
                    ) h WHERE h.rn = 1
                )
                SELECT
                    ISNULL(step1.UI_PreviousBalance, 0) AS UI_PreviousBalance,
                    ISNULL(step1.UI_InterestAmount, 0) AS UI_InterestAmount,
                    ISNULL(step1.UI_CurrentRent, ISNULL(cr.CurrentRent, 0)) AS UI_CurrentRent,
                    ISNULL(step1.UI_Balance, 0) AS UI_Balance,
                    
                    ISNULL(c.payment_identifier, 0) AS PaymentIdentifier,
                    ISNULL((SELECT SUM(pending_surcharge) FROM rentals WHERE client_id = c.client_id AND active = 1), 0) AS PendingSurcharge
                FROM clients c
                LEFT JOIN rentals r ON c.client_id = r.client_id AND r.active = 1
                LEFT JOIN CurrentRentalAmount cr ON r.rental_id = cr.rental_id

                OUTER APPLY (
                    SELECT TOP 1
                        MonthYearDB = cmb.month_year,
                        NetBalance = cmb.balance - cmb.paid - cmb.advanced_payment
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                    ORDER BY cmb.id DESC
                ) latest_cmb

                OUTER APPLY (
                    SELECT TOP 1
                        Id = cmb.id,
                        PrevBalDB = ISNULL(cmb.previous_balance, 0),
                        IntsDB = ISNULL(cmb.interests, 0),
                        RentDB = CASE WHEN ISNULL(cmb.monthly_debits, 0) = 0 THEN ISNULL(cr.CurrentRent, 0) ELSE cmb.monthly_debits END,
                        PaidDB = ISNULL(cmb.paid, 0),
                        AdvPayDB = ISNULL(cmb.advanced_payment, 0),
                        MonthYearDB = cmb.month_year
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                      AND (cmb.balance - cmb.paid - cmb.advanced_payment) > 0
                    ORDER BY cmb.id DESC
                ) db

                OUTER APPLY (
                    SELECT 
                        Raw_PrevBal = ISNULL((
                            SELECT SUM(
                                CASE
                                    WHEN ISNULL(cmb2.monthly_debits, 0) - ISNULL(cmb2.paid, 0) - ISNULL(cmb2.advanced_payment, 0) > 0
                                    THEN ISNULL(cmb2.monthly_debits, 0) - ISNULL(cmb2.paid, 0) - ISNULL(cmb2.advanced_payment, 0)
                                    ELSE 0
                                END
                            )
                            FROM client_month_balances cmb2
                            WHERE cmb2.rental_id = r.rental_id AND cmb2.id < db.Id
                        ), 0),
                        
                        Raw_Interest = ISNULL((
                            SELECT SUM(ISNULL(cmb2.interests, 0))
                            FROM client_month_balances cmb2
                            WHERE cmb2.rental_id = r.rental_id AND (cmb2.balance - cmb2.paid - cmb2.advanced_payment) > 0
                        ), 0),
                        
                        TotalPaid = ISNULL(db.PaidDB, 0) + ISNULL(db.AdvPayDB, 0)
                ) rawData

                OUTER APPLY (
                    SELECT Rem1 = CASE WHEN rawData.TotalPaid > rawData.Raw_PrevBal THEN rawData.TotalPaid - rawData.Raw_PrevBal ELSE 0 END
                ) calc1
                OUTER APPLY (
                    SELECT Rem2 = CASE WHEN calc1.Rem1 > db.RentDB THEN calc1.Rem1 - db.RentDB ELSE 0 END
                ) calc2
                OUTER APPLY (
                    SELECT UnpaidInts = CASE WHEN calc2.Rem2 > rawData.Raw_Interest THEN 0 ELSE rawData.Raw_Interest - calc2.Rem2 END
                ) calc3

                OUTER APPLY (
                    SELECT
                        UI_CurrentRent = db.RentDB,
                        UI_InterestAmount = calc3.UnpaidInts,
                        UI_Balance = -(db.PrevBalDB + db.IntsDB + db.RentDB - db.PaidDB - db.AdvPayDB),
                        UI_PreviousBalance = CASE 
                            WHEN ISNULL(db.AdvPayDB, 0) > 0 AND ISNULL(db.AdvPayDB, 0) < db.RentDB THEN ISNULL(db.AdvPayDB, 0)
                            ELSE -rawData.Raw_PrevBal
                        END
                ) step1
                WHERE c.client_id = @ClientId;";

            var dt = await _accessDB.GetTableAsync("Financial", query, [new SqlParameter("@ClientId", clientId)]);
            var data = new ClientFinancialDto();

            if (dt.Rows.Count == 0) return data;

            decimal uiPreviousBalance = Convert.ToDecimal(dt.Rows[0]["UI_PreviousBalance"]);
            decimal uiInterestAmount = Convert.ToDecimal(dt.Rows[0]["UI_InterestAmount"]);
            decimal uiCurrentRent = Convert.ToDecimal(dt.Rows[0]["UI_CurrentRent"]);
            decimal uiBalance = Convert.ToDecimal(dt.Rows[0]["UI_Balance"]);
            
            // CORRECCIÓN 1: ToDecimal en lugar de ToInt32 para mantener los ceros y centavos
            decimal paymentIdentifier = Convert.ToDecimal(dt.Rows[0]["PaymentIdentifier"]);
            decimal pendingSurcharge = Convert.ToDecimal(dt.Rows[0]["PendingSurcharge"]);

            // --- ESCENARIO 1: MES ACTUAL ---
            decimal currentMonthBaseDebt = uiBalance < 0 ? Math.Abs(uiBalance) : 0;
            
            // CORRECCIÓN 2: Invertimos el signo del saldo anterior. 
            // Si la UI mandaba negativo (Deuda), ahora es positivo. Si mandaba positivo (A favor), ahora es negativo.
            decimal currentMonthPrevBal = -uiPreviousBalance;

            if (!isNextMonth) 
            {
                data.PreviousBalance = currentMonthPrevBal;
                data.Surcharge = uiInterestAmount;
                data.CurrentBalance = currentMonthBaseDebt > 0 ? currentMonthBaseDebt + paymentIdentifier : 0;
                return data;
            }

            // --- ESCENARIO 2: PROYECCIÓN MES SIGUIENTE ---
            DateTime nextMonthDate = DateTime.Now.AddMonths(1);
            string nextMonthString = nextMonthDate.ToString("MM/yyyy");

            string checkQuery = @"
                SELECT 
                    -- Restamos el advanced_payment para que el saldo a favor baje correctamente como negativo
                    SUM(ISNULL(cmb.previous_balance, 0) - ISNULL(cmb.advanced_payment, 0)) AS PrevBal,
                    SUM(ISNULL(cmb.interests, 0)) AS Ints,
                    SUM(ISNULL(cmb.balance, 0) - ISNULL(cmb.paid, 0) - ISNULL(cmb.advanced_payment, 0)) AS NetBalance
                FROM client_month_balances cmb
                JOIN rentals r ON cmb.rental_id = r.rental_id
                WHERE r.client_id = @ClientId AND cmb.month_year = @MonthYear AND r.active = 1";

            var dtNext = await _accessDB.GetTableAsync("NextMonth", checkQuery, new[] { 
                new SqlParameter("@ClientId", clientId),
                new SqlParameter("@MonthYear", nextMonthString)
            });

            bool nextMonthExists = dtNext.Rows.Count > 0 && dtNext.Rows[0]["PrevBal"] != DBNull.Value;

            if (nextMonthExists)
            {
                data.PreviousBalance = Convert.ToDecimal(dtNext.Rows[0]["PrevBal"]);
                data.Surcharge = Convert.ToDecimal(dtNext.Rows[0]["Ints"]);
                
                decimal netBalanceNextMonth = Convert.ToDecimal(dtNext.Rows[0]["NetBalance"]);
                decimal nextMonthDebt = netBalanceNextMonth > 0 ? netBalanceNextMonth : 0;
                
                data.CurrentBalance = nextMonthDebt > 0 ? nextMonthDebt + paymentIdentifier : 0;
            }
            else
            {
                decimal projectedNextMonthRent = await CalculateProjectedNextMonthRentAsync(clientId);
                decimal totalProjectedDebt = 0;

                if (uiBalance < 0) 
                {
                    data.PreviousBalance = currentMonthPrevBal + uiCurrentRent;
                    data.Surcharge = uiInterestAmount + pendingSurcharge;
                    
                    totalProjectedDebt = data.PreviousBalance + data.Surcharge + projectedNextMonthRent;
                }
                else 
                {
                    // Como uiBalance es positivo (Saldo a Favor), le ponemos un menos adelante 
                    // para que se muestre en negativo en el comprobante del mes siguiente.
                    data.PreviousBalance = -uiBalance; 
                    data.Surcharge = 0;
                    
                    totalProjectedDebt = projectedNextMonthRent - uiBalance;
                    if (totalProjectedDebt < 0) totalProjectedDebt = 0;
                }

                data.CurrentBalance = totalProjectedDebt > 0 ? totalProjectedDebt + paymentIdentifier : 0;
            }

            return data;
        }

        private async Task<decimal> CalculateProjectedNextMonthRentAsync(int clientId)
        {
            decimal totalProjectedRent = 0;
            
            // Definimos los límites del próximo mes
            DateTime nextMonthFirstDay = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1);
            DateTime nextMonthLastDay = nextMonthFirstDay.AddMonths(1).AddDays(-1);

            // Modificamos la query para traer el método de pago usando un LEFT JOIN
            string query = @"
                SELECT 
                    r.rental_id, 
                    h.amount AS CurrentAmount, 
                    r.increase_anchor_date AS NextIncreaseDate,
                    ISNULL(pm.name, '') AS PaymentMethodName
                FROM rentals r
                JOIN rental_amount_history h ON r.rental_id = h.rental_id AND h.end_date IS NULL
                JOIN clients c ON r.client_id = c.client_id
                LEFT JOIN payment_methods pm ON c.preferred_payment_method_id = pm.payment_method_id
                WHERE r.client_id = @ClientId AND r.active = 1";

            var dt = await _accessDB.GetTableAsync("ActiveRentals", query, new[] { new SqlParameter("@ClientId", clientId) });

            foreach (DataRow row in dt.Rows)
            {
                decimal currentAmount = Convert.ToDecimal(row["CurrentAmount"]);
                DateTime nextIncreaseDate = row["NextIncreaseDate"] != DBNull.Value ? Convert.ToDateTime(row["NextIncreaseDate"]) : DateTime.MaxValue;
                string paymentMethodName = row["PaymentMethodName"].ToString().ToLower();

                // Verificamos si cae un aumento dentro de todo el mes proyectado
                if (nextIncreaseDate <= nextMonthLastDay)
                {
                    // Aplicamos el 10% de aumento directo
                    decimal increasedAmount = currentAmount * 1.10m;
                    
                    // LÓGICA DE REDONDEO DINÁMICO
                    if (paymentMethodName.Contains("efectivo"))
                    {
                        // Redondeo a los 1000 más cercanos (Efectivo)
                        totalProjectedRent += Math.Round(increasedAmount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
                    }
                    else
                    {
                        // Redondeo a los 100 más cercanos (Transferencia, Tarjeta, Otros)
                        totalProjectedRent += Math.Round(increasedAmount / 100m, MidpointRounding.AwayFromZero) * 100m;
                    }
                }
                else
                {
                    // Si no hay aumento este mes, paga lo normal (asumimos que ya viene redondeado del historial)
                    totalProjectedRent += currentAmount;
                }
            }

            return totalProjectedRent;
        }

        private decimal RoundToNearest1000(decimal amount)
        {
            if (amount == 0) return 0;

            // Math.Round con MidpointRounding.AwayFromZero asegura que los .5 suban.
            // Ej: 12500 / 1000 = 12.5 -> Round da 13 -> 13 * 1000 = 13000
            // Ej: 12499 / 1000 = 12.499 -> Round da 12 -> 12 * 1000 = 12000
            return Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
        }

        public async Task<bool> IsNextMonthStatementAsync(int communicationId)
        {
            string q = "SELECT is_next_month_statement FROM communications WHERE communication_id = @Id";
            var res = await _accessDB.ExecuteScalarAsync(q, [new SqlParameter("@Id", communicationId)]);
            return res != null && Convert.ToBoolean(res);
        }

        public async Task<bool> IsAccountStatementAsync(int communicationId) {
            string q = "SELECT is_account_statement FROM communications WHERE communication_id = @Id";
            var res = await _accessDB.ExecuteScalarAsync(q, [new SqlParameter("@Id", communicationId)]);
            return res != null && Convert.ToBoolean(res);
        }
    }
}
