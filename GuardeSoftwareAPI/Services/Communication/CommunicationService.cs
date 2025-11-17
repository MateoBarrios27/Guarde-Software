using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Jobs; // Assuming your Job is in a Jobs folder
using Microsoft.Data.SqlClient;
using Quartz;
using GuardeSoftwareAPI.Dtos.Communication;
using System.Text.Json; // Para serializar la lista de adjuntos

namespace GuardeSoftwareAPI.Services.communication
{
    public class CommunicationService : ICommunicationService
    {
        private readonly CommunicationDao _communicationDao;
        private readonly AccessDB _accessDB;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<CommunicationService> _logger;
        private readonly IFileStorageService _fileStorageService; // --- AÑADIDO ---

        public CommunicationService(
            AccessDB accessDB, 
            ISchedulerFactory schedulerFactory,
            ILogger<CommunicationService> logger,
            IFileStorageService fileStorageService // --- AÑADIDO ---
        )
        {
            _accessDB = accessDB;
            _communicationDao = new CommunicationDao(_accessDB);
            _schedulerFactory = schedulerFactory;
            _logger = logger;
            _fileStorageService = fileStorageService; // --- AÑADIDO ---
        }

        public async Task<List<CommunicationDto>> GetCommunications()
        {
            return await _communicationDao.GetCommunicationsAsync();
        }

        public async Task<CommunicationDto> GetCommunicationById(int id)
        {
            return await _communicationDao.GetCommunicationByIdAsync(id);
        }

        // --- MÉTODO ACTUALIZADO ---
        public async Task<CommunicationDto> CreateCommunicationAsync(UpsertCommunicationRequest request, List<AttachmentDto> uploadedFiles, int userId)
        {
            using (SqlConnection connection = _accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync())
                {
                    try
                    {
                        DateTime? scheduledAt = null;
                        if (request.Type == "schedule" && !string.IsNullOrEmpty(request.SendDate) && !string.IsNullOrEmpty(request.SendTime))
                        {
                            scheduledAt = DateTime.Parse($"{request.SendDate}T{request.SendTime}");
                        }
                        string status = request.Type == "schedule" ? "Scheduled" : "Draft";
                        
                        // 1. Crear registro principal
                        int newId = await _communicationDao.InsertCommunicationAsync(request, userId, scheduledAt, status, connection, transaction);

                        // 2. Serializar la lista de adjuntos a JSON
                        string attachmentsJson = JsonSerializer.Serialize(uploadedFiles);

                        // 3. Insertar canales (el DAO ahora aceptará el JSON de adjuntos)
                        foreach (var channel in request.Channels)
                        {
                            // Solo pasa los adjuntos si es el canal de Email
                            string channelAttachments = (channel == "Email") ? attachmentsJson : "[]";
                            await _communicationDao.InsertCommunicationChannelAsync(newId, channel, request, channelAttachments, connection, transaction);
                        }

                        // 4. Insertar destinatarios
                        await _communicationDao.InsertCommunicationRecipientsAsync(newId, request.Recipients, connection, transaction);

                        await transaction.CommitAsync();

                        // 5. Programar Job si es necesario
                        if (status == "Scheduled" && scheduledAt.HasValue)
                        {
                            await ScheduleJobAsync(newId, scheduledAt.Value, "default", false); // Usar servidor default
                        }

                        return await _communicationDao.GetCommunicationByIdAsync(newId);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        // --- IMPORTANTE: Borrar archivos del VPS si la transacción falla ---
                        await _fileStorageService.DeleteFilesAsync(uploadedFiles.Select(f => f.FileUrl).ToList());
                        _logger.LogError(ex, "Transaction failed for CREATE. Rolling back changes and deleting uploaded files.");
                        throw new Exception("Transaction failed. Rolling back changes.", ex);
                    }
                }
            }
        }

        // --- MÉTODO ACTUALIZADO ---
        public async Task<CommunicationDto> UpdateCommunicationAsync(int communicationId, UpsertCommunicationRequest request, List<AttachmentDto> newFiles, int userId)
        {
            // 1. Obtener la lista de archivos *antes* de la transacción
            var oldAttachments = await _communicationDao.GetAttachmentsAsync(communicationId);

            using (SqlConnection connection = _accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // 2. Borrar todos los hijos (canales, destinatarios, etc.)
                        await _communicationDao.DeleteCommunicationChildrenAsync(communicationId, connection, transaction);

                        // 3. Actualizar el principal
                        DateTime? scheduledAt = null;
                        if (request.Type == "schedule" && !string.IsNullOrEmpty(request.SendDate) && !string.IsNullOrEmpty(request.SendTime))
                        {
                            scheduledAt = DateTime.Parse($"{request.SendDate}T{request.SendTime}");
                        }
                        string status = request.Type == "schedule" ? "Scheduled" : "Draft";
                        
                        await _communicationDao.UpdateCommunicationMainAsync(communicationId, request, scheduledAt, status, connection, transaction);

                        // 4. Combinar listas de adjuntos
                        // (Aquí deberías implementar la lógica de 'AttachmentsToRemove' si la usas)
                        var finalAttachments = oldAttachments.Concat(newFiles).ToList();
                        string attachmentsJson = JsonSerializer.Serialize(finalAttachments);

                        // 5. Re-insertar canales
                        foreach (var channel in request.Channels)
                        {
                            string channelAttachments = (channel == "Email") ? attachmentsJson : "[]";
                            await _communicationDao.InsertCommunicationChannelAsync(communicationId, channel, request, channelAttachments, connection, transaction);
                        }

                        // 6. Re-insertar destinatarios
                        await _communicationDao.InsertCommunicationRecipientsAsync(communicationId, request.Recipients, connection, transaction);

                        await transaction.CommitAsync();

                        // 7. Re-programar job
                        if (status == "Scheduled" && scheduledAt.HasValue)
                        {
                            await ScheduleJobAsync(communicationId, scheduledAt.Value, "default", false);
                        }
                        
                        // 8. Borrar los archivos viejos del VPS (solo si el commit fue exitoso)
                        // (Aquí también iría la lógica de 'AttachmentsToRemove')
                        // await _fileStorageService.DeleteFilesAsync(filesToDelete);

                        return await _communicationDao.GetCommunicationByIdAsync(communicationId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Transaction failed for UPDATE on ID: {CommunicationId}", communicationId);
                        try { await transaction.RollbackAsync(); }
                        catch (Exception rbEx) { _logger.LogWarning(rbEx, "Error during update rollback."); }
                        
                        // Si la transacción falló, borra los *nuevos* archivos que se subieron
                        await _fileStorageService.DeleteFilesAsync(newFiles.Select(f => f.FileUrl).ToList());
                        
                        throw new Exception("Transaction failed. Rolling back changes.", ex);
                    }
                }
            }
        }

        // --- MÉTODO ACTUALIZADO ---
        public async Task<bool> DeleteCommunicationAsync(int communicationId)
        {
            // 1. Obtener la lista de archivos ANTES de borrar
            var attachments = await _communicationDao.GetAttachmentsAsync(communicationId);

            // 2. Borrar de la DB (tu DAO ya hace esto en cascada)
            bool success = await _communicationDao.DeleteCommunicationAsync(communicationId);

            if (success)
            {
                // 3. Si se borró de la DB, borrar los archivos del VPS
                var urlsToDelete = attachments.Select(a => a.FileUrl).ToList();
                await _fileStorageService.DeleteFilesAsync(urlsToDelete);
            }
            return success;
        }

        // --- MÉTODO NUEVO ---
        public async Task DeleteAttachmentAsync(int communicationId, string fileName)
        {
            // 1. Borrar del VPS
            // (Asumimos que fileName es único, o que fileUrl se pasa como 'fileName')
            await _fileStorageService.DeleteFileAsync(fileName);
            
            // 2. Borrar de la DB (actualizando el JSON)
            await _communicationDao.RemoveAttachmentFromJsonAsync(communicationId, fileName);
        }

        // --- MÉTODO ACTUALIZADO (Ahora es 'async') ---
        private async Task ScheduleJobAsync(int communicationId, DateTime runTime, string mailServerId, bool isRetry)
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobIdentity = $"comm-job-{communicationId}";
            
            // Cancela cualquier job existente para este ID (ej. si re-programan)
            await scheduler.DeleteJob(new JobKey(jobIdentity));

            var job = JobBuilder.Create<SendCommunicationJob>()
                .WithIdentity(jobIdentity)
                .UsingJobData("CommunicationId", communicationId)
                .UsingJobData("MailServerId", mailServerId) // Pasa el ID del servidor
                .UsingJobData("IsRetry", isRetry) // Pasa el flag de reintento
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"comm-trigger-{communicationId}")
                .StartAt(runTime)
                .Build();

            await scheduler.ScheduleJob(job, trigger);
        }
        
        // --- MÉTODO ACTUALIZADO ---
        public async Task<CommunicationDto> SendDraftNowAsync(int communicationId)
        {
            var scheduleTime = DateTime.Now.AddSeconds(10); // 10 segundos para que Quartz lo tome rápido

            bool success = await _communicationDao.UpdateCommunicationStatusAndDateAsync(communicationId, "Scheduled", scheduleTime);

            if (success)
            {
                // Envía usando el servidor "default" y sin modo "retry"
                await ScheduleJobAsync(communicationId, scheduleTime, "default", false);
                return await _communicationDao.GetCommunicationByIdAsync(communicationId);
            }
            throw new Exception("Failed to update status for sending.");
        }
        
        // --- MÉTODO NUEVO ---
        public async Task<CommunicationDto> RetryFailedSendsAsync(int communicationId, string mailServerId)
        {
            var scheduleTime = DateTime.Now.AddSeconds(10); // Reintentar ahora
            
            // 1. Actualiza estado a "Processing" o "Scheduled"
            bool success = await _communicationDao.UpdateCommunicationStatusAndDateAsync(communicationId, "Scheduled", scheduleTime);

            if (success)
            {
                // 2. Encola el job CON el nuevo serverId y el flag de reintento
                await ScheduleJobAsync(communicationId, scheduleTime, mailServerId, true);
                return await _communicationDao.GetCommunicationByIdAsync(communicationId);
            }
            throw new Exception("Failed to update status for retry.");
        }

        public async Task<List<ClientCommunicationDto>> GetCommunicationsByClientIdAsync(int clientId)
        {
            if (clientId <= 0)
            {
                _logger.LogWarning("Se solicitó historial de comunicación para un ID de cliente inválido: {ClientId}", clientId);
                return new List<ClientCommunicationDto>();
            }
            try
            {
                return await _communicationDao.GetCommunicationsByClientIdAsync(clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de comunicación para el cliente ID {ClientId}", clientId);
                throw;
            }
        }
    }
}