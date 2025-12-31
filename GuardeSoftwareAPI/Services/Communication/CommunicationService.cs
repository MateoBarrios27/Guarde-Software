using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Jobs; // Assuming your Job is in a Jobs folder
using Microsoft.Data.SqlClient;
using Quartz;
using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Services.communication
{
    public class CommunicationService : ICommunicationService
    {
        private readonly CommunicationDao _communicationDao;
        private readonly AccessDB accessDB; // To get the connection
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<CommunicationService> logger;

        public CommunicationService(
            AccessDB _accessDB, // Inject your AccessDB
            ISchedulerFactory schedulerFactory,
            ILogger<CommunicationService> _logger)
        {
            _communicationDao = new CommunicationDao(_accessDB);
            accessDB = _accessDB;
            _schedulerFactory = schedulerFactory;
            logger = _logger;
        }

        public async Task<List<CommunicationDto>> GetCommunications()
        {
            return await _communicationDao.GetCommunicationsAsync();
        }

        public async Task<CommunicationDto> GetCommunicationById(int id)
        {
            return await _communicationDao.GetCommunicationByIdAsync(id);
        }

        /// <summary>
        /// Creates a new communication using a database transaction.
        /// </summary>
        public async Task<CommunicationDto> CreateCommunicationAsync(UpsertCommunicationRequest request, int userId)
        {
            // Use your AccessDB method to get a connection
            using (SqlConnection connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                
                // Start the transaction
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // --- Transactional Steps ---
                        
                        DateTime? scheduledAt = null;
                        if (request.Type == "schedule" && !string.IsNullOrEmpty(request.SendDate) && !string.IsNullOrEmpty(request.SendTime))
                        {
                            scheduledAt = DateTime.Parse($"{request.SendDate}T{request.SendTime}");
                        }
                        string status = request.Type == "schedule" ? "Scheduled" : "Draft";
                        
                        // Step 1: Create main record, get new ID
                        int newId = await _communicationDao.InsertCommunicationAsync(request, userId, scheduledAt, status, connection, transaction);

                        // Step 2: Loop and insert channels
                        foreach (var channel in request.Channels)
                        {
                            await _communicationDao.InsertCommunicationChannelAsync(newId, channel, request, connection, transaction);
                        }

                        // Step 3: Insert all recipients
                        await _communicationDao.InsertCommunicationRecipientsAsync(newId, request.Recipients, connection, transaction);

                        if (request.Attachments != null && request.Attachments.Count > 0)
                        {
                            var savedAttachments = new List<AttachmentDto>();
                            // Definir ruta. En Linux VPS asegúrate que wwwroot/uploads tenga permisos (chmod 755)
                            string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "communications");
                            if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                            foreach (var file in request.Attachments)
                            {
                                string uniqueName = $"{Guid.NewGuid()}_{file.FileName}";
                                string filePath = Path.Combine(uploadFolder, uniqueName);

                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                savedAttachments.Add(new AttachmentDto {
                                    FileName = file.FileName,
                                    FilePath = filePath,
                                    ContentType = file.ContentType
                                });
                            }
                            
                            // Guardar referencia en BD
                            await _communicationDao.InsertAttachmentsAsync(newId, savedAttachments, connection, transaction);
                        }

                        // --- End Transaction ---

                        // If all steps succeeded, commit the transaction
                        await transaction.CommitAsync();

                        // Schedule Quartz job (only after commit is successful)
                        if (status == "Scheduled" && scheduledAt.HasValue)
                        {
                            await ScheduleJobAsync(newId, scheduledAt.Value);
                        }

                        // Return the newly created DTO (read is outside transaction)
                        return await _communicationDao.GetCommunicationByIdAsync(newId);
                    }
                    catch (Exception ex)
                    {
                        // If any step failed, roll back all changes
                        await transaction.RollbackAsync();
                        // Log the error (optional)
                        throw new Exception("Transaction failed. Rolling back changes.", ex);
                    }
                }
            } // Connection is automatically closed by 'using'
        }

        private async Task ScheduleJobAsync(int communicationId, DateTime runTime)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            var job = JobBuilder.Create<SendCommunicationJob>() // Use your actual Job class
                .WithIdentity($"comm-job-{communicationId}")
                .UsingJobData("CommunicationId", communicationId)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"comm-trigger-{communicationId}")
                .StartAt(runTime)
                .Build();

            await scheduler.ScheduleJob(job, trigger);
        }
        
        public async Task<CommunicationDto> UpdateCommunicationAsync(int communicationId, UpsertCommunicationRequest request, int userId)
{
    using (SqlConnection connection = accessDB.GetConnectionClose())
    {
        await connection.OpenAsync();
        using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync())
        {
            try
            {
                // --- 1. Lógica de Fechas y Estado ---
                DateTime? scheduledAt = null;
                string requestType = request.Type?.ToLower().Trim() ?? "draft";
                string status = "Draft";

                // Parsear fecha si existe
                if (!string.IsNullOrEmpty(request.SendDate) && !string.IsNullOrEmpty(request.SendTime))
                {
                    if (DateTime.TryParse($"{request.SendDate}T{request.SendTime}", out DateTime parsedDate))
                    {
                        scheduledAt = parsedDate;
                    }
                }

                // Determinar estado final
                if (requestType == "schedule" && scheduledAt.HasValue)
                {
                    status = "Scheduled";
                }
                else
                {
                    status = "Draft";
                    scheduledAt = null;
                }

                // --- 2. Actualizaciones en Base de Datos ---

                // A. Actualizar Tabla Principal
                string updateQuery = @"
                    UPDATE communications 
                    SET title = @Title, 
                        scheduled_date = @ScheduledDate, 
                        status = @Status,
                        smtp_configuration_id = @SmtpConfigId
                    WHERE communication_id = @Id";
                
                using (var cmdUpdate = new SqlCommand(updateQuery, connection, transaction))
                {
                    cmdUpdate.Parameters.AddWithValue("@Id", communicationId);
                    cmdUpdate.Parameters.AddWithValue("@Title", request.Title);
                    cmdUpdate.Parameters.AddWithValue("@ScheduledDate", (object)scheduledAt ?? DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@Status", status);
                    cmdUpdate.Parameters.AddWithValue("@SmtpConfigId", (object)request.SmtpConfigId ?? DBNull.Value);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                // B. Actualizar Contenido (Sin borrar, para no romper FK de dispatches)
                foreach (var channel in request.Channels)
                {
                    // Intenta actualizar primero
                    string updateContentQuery = @"
                        UPDATE communication_channel_content 
                        SET content = @Content, subject = @Subject
                        WHERE communication_id = @Id 
                        AND channel_id = (SELECT channel_id FROM communication_channels WHERE name = @ChannelName)";

                    using (var cmdContent = new SqlCommand(updateContentQuery, connection, transaction))
                    {
                        cmdContent.Parameters.AddWithValue("@Id", communicationId);
                        cmdContent.Parameters.AddWithValue("@ChannelName", channel);
                        cmdContent.Parameters.AddWithValue("@Subject", channel == "Email" ? (object)request.Title : DBNull.Value);
                        cmdContent.Parameters.AddWithValue("@Content", request.Content);
                        
                        int rows = await cmdContent.ExecuteNonQueryAsync();

                        // Si rows es 0, significa que este canal no existía, así que lo insertamos
                        if (rows == 0)
                        {
                            await _communicationDao.InsertCommunicationChannelAsync(communicationId, channel, request, connection, transaction);
                        }
                    }
                }

                // C. Actualizar Destinatarios
                // Aquí SI borramos y recreamos porque queremos actualizar la lista de objetivos.
                // El historial (dispatches) no se rompe porque apunta al channel_content_id (que preservamos arriba).
                string deleteRecipients = "DELETE FROM communication_recipients WHERE communication_id = @Id";
                using (var cmdDel = new SqlCommand(deleteRecipients, connection, transaction))
                {
                    cmdDel.Parameters.AddWithValue("@Id", communicationId);
                    await cmdDel.ExecuteNonQueryAsync();
                }
                
                await _communicationDao.InsertCommunicationRecipientsAsync(communicationId, request.Recipients, connection, transaction);

                // D. Manejo de Adjuntos (Opcional: Agregar nuevos)
                if (request.Attachments != null && request.Attachments.Count > 0)
                {
                    var savedAttachments = new List<AttachmentDto>();
                    string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "communications");
                    if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                    foreach (var file in request.Attachments)
                    {
                        string uniqueName = $"{Guid.NewGuid()}_{file.FileName}";
                        string filePath = Path.Combine(uploadFolder, uniqueName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        savedAttachments.Add(new AttachmentDto {
                            FileName = file.FileName,
                            FilePath = filePath,
                            ContentType = file.ContentType
                        });
                    }
                    await _communicationDao.InsertAttachmentsAsync(communicationId, savedAttachments, connection, transaction);
                }

                await transaction.CommitAsync();

                // --- 3. Gestión de Quartz (CORRECCIÓN CRÍTICA) ---
                
                // Primero: Obtener el scheduler
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKey = new JobKey($"comm-job-{communicationId}");

                // Si existe un job previo, LO BORRAMOS primero para evitar "ObjectAlreadyExistsException"
                if (await scheduler.CheckExists(jobKey))
                {
                    await scheduler.DeleteJob(jobKey);
                }

                // Si el nuevo estado es Scheduled, creamos el nuevo Job
                if (status == "Scheduled" && scheduledAt.HasValue)
                {
                    await ScheduleJobAsync(communicationId, scheduledAt.Value);
                }
                
                return await _communicationDao.GetCommunicationByIdAsync(communicationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transaction failed for UPDATE on ID: {CommunicationId}", communicationId);
                try { await transaction.RollbackAsync(); }
                catch (Exception rbEx) { logger.LogWarning(rbEx, "Error during update rollback."); }
                throw; // Re-lanzar la excepción original para ver el error real
            }
        }
    }
}

        public async Task<bool> DeleteCommunicationAsync(int communicationId)
        {
            // You already implemented this, but I include it for completeness
            return await _communicationDao.DeleteCommunicationAsync(communicationId);
        }

        public async Task<CommunicationDto> SendDraftNowAsync(int communicationId)
        {
            // To 'send now', we set its status to 'Scheduled'
            // and the date to 1 minute from now, so Quartz can pick it up.
            var scheduleTime = DateTime.Now.AddMinutes(1);

            bool success = await _communicationDao.UpdateCommunicationStatusAndDateAsync(communicationId, "Scheduled", scheduleTime);

            if (success)
            {
                await ScheduleJobAsync(communicationId, scheduleTime);
                return await _communicationDao.GetCommunicationByIdAsync(communicationId);
            }

            throw new Exception("Failed to update status for sending.");
        }
        
        public async Task<List<ClientCommunicationDto>> GetCommunicationsByClientIdAsync(int clientId)
        {
            if (clientId <= 0)
            {
                logger.LogWarning("Se solicitó historial de comunicación para un ID de cliente inválido: {ClientId}", clientId);
                return new List<ClientCommunicationDto>(); // Devolver lista vacía
            }
            try
            {
                return await _communicationDao.GetCommunicationsByClientIdAsync(clientId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al obtener historial de comunicación para el cliente ID {ClientId}", clientId);
                throw; // Re-lanza para que el controlador lo capture
            }
        }

        public async Task<List<ClientRecipientDto>> GetClientsForSelectorAsync()
        {
            return await _communicationDao.GetClientsForSelectorAsync();
        }
    }
}