using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Jobs; // Assuming your Job is in a Jobs folder
using Microsoft.Data.SqlClient;
using System.Data;
using Quartz;
using GuardeSoftwareAPI.Dtos.Communication;
using Microsoft.AspNetCore.SignalR;
using GuardeSoftwareAPI.Hubs;

namespace GuardeSoftwareAPI.Services.communication
{
    public class CommunicationService : ICommunicationService
    {
        private readonly CommunicationDao _communicationDao;
        private readonly AccessDB accessDB; // To get the connection
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<CommunicationService> logger;
        private readonly IHubContext<CommunicationHub> _hubContext;

        public CommunicationService(
            AccessDB _accessDB, // Inject your AccessDB
            ISchedulerFactory schedulerFactory,
            ILogger<CommunicationService> _logger,
            IHubContext<CommunicationHub> hubContext)
        {
            _communicationDao = new CommunicationDao(_accessDB);
            accessDB = _accessDB;
            _schedulerFactory = schedulerFactory;
            logger = _logger;
            _hubContext = hubContext;
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
            NormalizeAndValidateRecipientScope(request);

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

                        // Step 3: Insert the selected recipient scope
                        if (request.SendToAllEmails)
                        {
                            await _communicationDao.InsertAllEmailRecipientsAsync(newId, connection, transaction);
                        }
                        else
                        {
                            await _communicationDao.InsertCommunicationRecipientsAsync(newId, request.Recipients, connection, transaction);
                            int insertedExternalRecipients = await _communicationDao.InsertCommunicationMassRecipientsAsync(
                                newId,
                                request.ExternalRecipientIds,
                                connection,
                                transaction);

                            if (insertedExternalRecipients != request.ExternalRecipientIds.Count)
                            {
                                throw new InvalidOperationException(
                                    "Uno o más receptores externos seleccionados ya no están activos o no tienen email.");
                            }
                        }

                        if (request.Attachments != null && request.Attachments.Count > 0)
                        {
                            var savedAttachments = new List<AttachmentDto>();
                            // Definir ruta. En Linux VPS asegurar que wwwroot/uploads tenga permisos (chmod 755)
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
                            await ScheduleJobAsync(newId, scheduledAt.Value, request.IsTestMode, request.TestEmailAddress);
                        }

                        // Return the newly created DTO (read is outside transaction)
                        var newCommunication = await _communicationDao.GetCommunicationByIdAsync(newId);
                        
                        await _hubContext.Clients.All.SendAsync("CommunicationUpdated", newId);

                        return newCommunication;
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

        private static void NormalizeAndValidateRecipientScope(UpsertCommunicationRequest request)
        {
            request.Recipients ??= [];
            request.Channels ??= [];
            request.ExternalRecipientIds = (request.ExternalRecipientIds ?? [])
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            bool hasEmailChannel = request.Channels.Contains("Email", StringComparer.OrdinalIgnoreCase);

            if (request.SendToAllEmails)
            {
                if (!hasEmailChannel)
                {
                    throw new InvalidOperationException("La selección de todos los emails requiere el canal Email.");
                }

                request.ExternalRecipientIds = [];
                return;
            }

            if (request.ExternalRecipientIds.Count > 0)
            {
                if (!hasEmailChannel)
                {
                    throw new InvalidOperationException("Los receptores externos por rubro sólo pueden recibir el comunicado por Email.");
                }

                if (request.IsAccountStatement)
                {
                    throw new InvalidOperationException("Los estados de cuenta sólo pueden enviarse a clientes.");
                }
            }

            if (request.Recipients.Count == 0 && request.ExternalRecipientIds.Count == 0)
            {
                throw new InvalidOperationException("Debés seleccionar al menos un destinatario.");
            }
        }

        private async Task ScheduleJobAsync(int communicationId, DateTime runTime, bool isTestMode = false, string? testEmail = null)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            string jobSuffix = isTestMode ? $"-test-{Guid.NewGuid()}" : "";
            var job = JobBuilder.Create<SendCommunicationJob>() // Use your actual Job class
                .WithIdentity($"comm-job-{communicationId}{jobSuffix}")
                .UsingJobData("CommunicationId", communicationId)
                .UsingJobData("IsTestMode", isTestMode)
                .UsingJobData("TestEmailAddress", testEmail ?? "")
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"comm-trigger-{communicationId}")
                .StartAt(runTime)
                .Build();

            await scheduler.ScheduleJob(job, trigger);
        }
        
        //Requiere logica de guardar archivos adjuntos
        public async Task<CommunicationDto> UpdateCommunicationAsync(int communicationId, UpsertCommunicationRequest request, int userId)
        {
            NormalizeAndValidateRecipientScope(request);

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
                                smtp_configuration_id = @SmtpConfigId,
                                is_account_statement = @IsAccountStatement,
                                is_next_month_statement = @IsNextMonthStatement,
                                send_to_all_emails = @SendToAllEmails
                            WHERE communication_id = @Id";
                        
                        using (var cmdUpdate = new SqlCommand(updateQuery, connection, transaction))
                        {
                            cmdUpdate.Parameters.AddWithValue("@Id", communicationId);
                            cmdUpdate.Parameters.AddWithValue("@Title", request.Title);
                            cmdUpdate.Parameters.AddWithValue("@ScheduledDate", (object)scheduledAt ?? DBNull.Value);
                            cmdUpdate.Parameters.AddWithValue("@Status", status);
                            cmdUpdate.Parameters.AddWithValue("@SmtpConfigId", (object)request.SmtpConfigId ?? DBNull.Value);
                            cmdUpdate.Parameters.AddWithValue("@IsAccountStatement", request.IsAccountStatement);
                            cmdUpdate.Parameters.AddWithValue("@IsNextMonthStatement", request.IsNextMonthStatement);
                            cmdUpdate.Parameters.AddWithValue("@SendToAllEmails", request.SendToAllEmails);
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

                        string deleteExternalRecipients = "DELETE FROM communication_mass_recipients WHERE communication_id = @Id";
                        using (var cmdDelExternal = new SqlCommand(deleteExternalRecipients, connection, transaction))
                        {
                            cmdDelExternal.Parameters.AddWithValue("@Id", communicationId);
                            await cmdDelExternal.ExecuteNonQueryAsync();
                        }
                        
                        if (request.SendToAllEmails)
                        {
                            await _communicationDao.InsertAllEmailRecipientsAsync(communicationId, connection, transaction);
                        }
                        else
                        {
                            await _communicationDao.InsertCommunicationRecipientsAsync(communicationId, request.Recipients, connection, transaction);
                            int insertedExternalRecipients = await _communicationDao.InsertCommunicationMassRecipientsAsync(
                                communicationId,
                                request.ExternalRecipientIds,
                                connection,
                                transaction);

                            if (insertedExternalRecipients != request.ExternalRecipientIds.Count)
                            {
                                throw new InvalidOperationException(
                                    "Uno o más receptores externos seleccionados ya no están activos o no tienen email.");
                            }
                        }

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
                            await ScheduleJobAsync(communicationId, scheduledAt.Value, request.IsTestMode, request.TestEmailAddress);
                        }
                        
                        var newCommunication = await _communicationDao.GetCommunicationByIdAsync(communicationId);
                        
                        await _hubContext.Clients.All.SendAsync("CommunicationUpdated", communicationId);

                        return newCommunication;
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
            var result = await _communicationDao.DeleteCommunicationAsync(communicationId);
            if (result)
            {
                await _hubContext.Clients.All.SendAsync("CommunicationUpdated", communicationId);
            }
            return result;
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
                await _hubContext.Clients.All.SendAsync("CommunicationUpdated", communicationId);
                return await _communicationDao.GetCommunicationByIdAsync(communicationId);
            }

            throw new Exception("Failed to update status for sending.");
        }

        public async Task<CommunicationDto> RetrySelectedFailedCommunicationAsync(
            int communicationId,
            List<int> selectedClientIds,
            List<int>? selectedExternalRecipientIds = null)
        {
            await _communicationDao.UpdateRecipientsRetrySelectionAsync(
                communicationId,
                selectedClientIds,
                selectedExternalRecipientIds);
            return await SendDraftNowAsync(communicationId);
        }

        public async Task<CommunicationExtensionPreviewDto> GetCommunicationExtensionPreviewAsync(
            int communicationId,
            string recipientType,
            string mode)
        {
            string normalizedType = NormalizeExtensionRecipientType(recipientType);
            string normalizedMode = NormalizeExtensionMode(mode);
            CommunicationExtensionSourceData? source =
                await _communicationDao.GetCommunicationExtensionSourceDataAsync(
                    communicationId,
                    normalizedType);

            ValidateExtensionSource(source);
            return BuildExtensionPreview(source!, normalizedType, normalizedMode);
        }

        public async Task<CommunicationExtensionResultDto> ExtendCommunicationAsync(
            int communicationId,
            ExtendCommunicationRequest request,
            int userId)
        {
            if (request is null)
            {
                throw new ArgumentException("Los datos para ampliar el comunicado son obligatorios.");
            }

            string normalizedType = NormalizeExtensionRecipientType(request.RecipientType);
            string normalizedMode = NormalizeExtensionMode(request.Mode);

            using SqlConnection connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                await AcquireCommunicationExtensionLockAsync(connection, transaction, communicationId);

                CommunicationExtensionSourceData? source =
                    await _communicationDao.GetCommunicationExtensionSourceDataAsync(
                        communicationId,
                        normalizedType,
                        connection,
                        transaction);

                ValidateExtensionSource(source);
                CommunicationExtensionPreviewDto preview =
                    BuildExtensionPreview(source!, normalizedType, normalizedMode);

                if (preview.SelectedForSendCount == 0)
                {
                    await transaction.CommitAsync();
                    return ToExtensionResult(preview, queued: false, addedAssociationCount: 0, communication: null);
                }

                List<int> selectedRecipientIds = source!.Recipients
                    .Where(recipient => IsSelectedForExtension(recipient, normalizedMode))
                    .Select(recipient => recipient.Id)
                    .ToList();

                int addedAssociationCount = await _communicationDao.AddExternalRecipientsToCommunicationAsync(
                    communicationId,
                    source.EmailChannelContentId!.Value,
                    selectedRecipientIds,
                    connection,
                    transaction);

                await _communicationDao.SetExternalRecipientRetryScopeAsync(
                    source.EmailChannelContentId.Value,
                    selectedRecipientIds,
                    connection,
                    transaction);

                DateTime scheduledDate = DateTime.Now.AddMinutes(1);
                bool scheduled = await _communicationDao.ScheduleExternalCommunicationExtensionAsync(
                    communicationId,
                    scheduledDate,
                    connection,
                    transaction);

                if (!scheduled)
                {
                    throw new InvalidOperationException(
                        "El comunicado cambió de estado mientras se intentaba ampliarlo. Volvé a abrirlo y revisá el detalle.");
                }

                await transaction.CommitAsync();

                await ScheduleJobAsync(communicationId, scheduledDate);
                CommunicationDto communication = await _communicationDao.GetCommunicationByIdAsync(communicationId);
                await _hubContext.Clients.All.SendAsync("CommunicationUpdated", communicationId);

                return ToExtensionResult(
                    preview,
                    queued: true,
                    addedAssociationCount,
                    communication);
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackException)
                {
                    logger.LogWarning(
                        rollbackException,
                        "No se pudo revertir la ampliación del comunicado {CommunicationId}.",
                        communicationId);
                }

                throw;
            }
        }

        private static string NormalizeExtensionRecipientType(string? recipientType)
        {
            string normalized = recipientType?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                throw new ArgumentException("Seleccioná un rubro de receptores externos.");
            }

            if (normalized.Length > 100)
            {
                throw new ArgumentException("El rubro no puede superar los 100 caracteres.");
            }

            return normalized;
        }

        private static string NormalizeExtensionMode(string? mode)
        {
            string normalized = mode?.Trim().ToLowerInvariant() ?? string.Empty;
            return normalized switch
            {
                CommunicationExtensionModes.NeverAttempted => normalized,
                CommunicationExtensionModes.WithoutSuccessfulDelivery => normalized,
                _ => throw new ArgumentException("El modo de ampliación seleccionado no es válido.")
            };
        }

        private static void ValidateExtensionSource(CommunicationExtensionSourceData? source)
        {
            if (source is null)
            {
                throw new KeyNotFoundException("No se encontró el comunicado seleccionado.");
            }

            if (!source.Status.Equals("Finished", StringComparison.OrdinalIgnoreCase)
                && !source.Status.Equals("Finished w/ Errors", StringComparison.OrdinalIgnoreCase)
                && !source.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Sólo se puede ampliar un comunicado que ya terminó de procesarse.");
            }

            if (source.SendToAllEmails)
            {
                throw new InvalidOperationException(
                    "Este comunicado usa 'Todos los emails'. Para ampliar por rubro, utilizá un comunicado con receptores externos seleccionados.");
            }

            if (source.IsAccountStatement)
            {
                throw new InvalidOperationException(
                    "Los estados de cuenta no se pueden ampliar con receptores externos.");
            }

            if (source.HasWhatsAppChannel)
            {
                throw new InvalidOperationException(
                    "La ampliación por rubro sólo está disponible para comunicados de Email.");
            }

            if (source.ClientRecipientCount > 0)
            {
                throw new InvalidOperationException(
                    "Este comunicado también tiene clientes seleccionados. Creá una campaña externa separada para evitar reenvíos fuera del rubro.");
            }

            if (!source.EmailChannelContentId.HasValue)
            {
                throw new InvalidOperationException("El comunicado no tiene contenido de Email para reutilizar.");
            }

            if (IsTestCommunicationTitle(source.Title))
            {
                throw new InvalidOperationException(
                    "No se puede ampliar una campaña de prueba. Creá una campaña real antes de enviarla a los nuevos receptores.");
            }
        }

        private static CommunicationExtensionPreviewDto BuildExtensionPreview(
            CommunicationExtensionSourceData source,
            string recipientType,
            string mode)
        {
            List<CommunicationExtensionRecipientDto> eligibleRecipients = source.Recipients
                .Where(recipient => recipient.IsActive && !string.IsNullOrWhiteSpace(recipient.Email))
                .ToList();

            List<CommunicationExtensionRecipientDto> selectedRecipients = eligibleRecipients
                .Where(recipient => IsSelectedForExtension(recipient, mode))
                .ToList();

            return new CommunicationExtensionPreviewDto
            {
                CommunicationId = source.CommunicationId,
                Title = source.Title,
                Status = source.Status,
                RecipientType = recipientType,
                Mode = mode,
                TotalInDirectory = source.Recipients.Count,
                EligibleWithEmail = eligibleRecipients.Count,
                AlreadySuccessfulCount = eligibleRecipients.Count(recipient => recipient.HasRealSuccess),
                NeverAttemptedCount = eligibleRecipients.Count(recipient => !recipient.HasRealAttempt),
                PreviouslyAttemptedCount = eligibleRecipients.Count(recipient => recipient.HasRealAttempt),
                FailedOrPendingCount = eligibleRecipients.Count(recipient => recipient.HasRealAttempt && !recipient.HasRealSuccess),
                AlreadyAssociatedCount = eligibleRecipients.Count(recipient => recipient.IsAssociated),
                NewToCommunicationCount = eligibleRecipients.Count(recipient => !recipient.IsAssociated),
                SelectedForSendCount = selectedRecipients.Count,
                InactiveOrWithoutEmailCount = source.Recipients.Count - eligibleRecipients.Count,
                IsTestCommunication = IsTestCommunicationTitle(source.Title),
                CandidateListTruncated = selectedRecipients.Count > 200,
                Recipients = selectedRecipients.Take(200).ToList()
            };
        }

        private static bool IsSelectedForExtension(
            CommunicationExtensionRecipientDto recipient,
            string mode)
        {
            if (!recipient.IsActive || string.IsNullOrWhiteSpace(recipient.Email)) return false;

            return mode == CommunicationExtensionModes.WithoutSuccessfulDelivery
                ? !recipient.HasRealSuccess
                : !recipient.HasRealAttempt;
        }

        private static bool IsTestCommunicationTitle(string? title)
        {
            return title?.TrimStart().StartsWith("[PRUEBA]", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static CommunicationExtensionResultDto ToExtensionResult(
            CommunicationExtensionPreviewDto preview,
            bool queued,
            int addedAssociationCount,
            CommunicationDto? communication)
        {
            return new CommunicationExtensionResultDto
            {
                CommunicationId = preview.CommunicationId,
                Title = preview.Title,
                Status = preview.Status,
                RecipientType = preview.RecipientType,
                Mode = preview.Mode,
                TotalInDirectory = preview.TotalInDirectory,
                EligibleWithEmail = preview.EligibleWithEmail,
                AlreadySuccessfulCount = preview.AlreadySuccessfulCount,
                NeverAttemptedCount = preview.NeverAttemptedCount,
                PreviouslyAttemptedCount = preview.PreviouslyAttemptedCount,
                FailedOrPendingCount = preview.FailedOrPendingCount,
                AlreadyAssociatedCount = preview.AlreadyAssociatedCount,
                NewToCommunicationCount = preview.NewToCommunicationCount,
                SelectedForSendCount = preview.SelectedForSendCount,
                InactiveOrWithoutEmailCount = preview.InactiveOrWithoutEmailCount,
                IsTestCommunication = preview.IsTestCommunication,
                CandidateListTruncated = preview.CandidateListTruncated,
                Recipients = preview.Recipients,
                Queued = queued,
                AddedAssociationCount = addedAssociationCount,
                Communication = communication
            };
        }

        private static async Task AcquireCommunicationExtensionLockAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            int communicationId)
        {
            const string query = @"
                DECLARE @LockResult INT;
                EXEC @LockResult = sp_getapplock
                    @Resource = @Resource,
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction',
                    @LockTimeout = 10000;
                IF @LockResult < 0
                    THROW 51002, 'No se pudo obtener el bloqueo para ampliar el comunicado.', 1;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
            {
                Value = $"CommunicationExtension-{communicationId}"
            });
            await command.ExecuteNonQueryAsync();
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

        public async Task<string?> GetDispatchContentAsync(int dispatchId)
        {
            return await _communicationDao.GetDispatchContentAsync(dispatchId);
        }
    }
}
