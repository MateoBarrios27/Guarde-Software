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
            // An 'Update' is complex. It's safer to treat it as a 'Delete children + Re-create children' transaction.
            using (SqlConnection connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // Step 1: Delete all child records
                        string deleteQuery = @"
                            DELETE FROM dispatches WHERE comm_channel_content_id IN (SELECT comm_channel_content_id FROM communication_channel_content WHERE communication_id = @Id);
                            DELETE FROM communication_recipients WHERE communication_id = @Id;
                            DELETE FROM communication_channel_content WHERE communication_id = @Id;
                        ";
                        using (var cmdDelete = new SqlCommand(deleteQuery, connection, transaction))
                        {
                            cmdDelete.Parameters.AddWithValue("@Id", communicationId);
                            await cmdDelete.ExecuteNonQueryAsync();
                        }

                        // Step 2: Update the main communication record
                        DateTime? scheduledAt = null;
                        if (request.Type == "schedule" && !string.IsNullOrEmpty(request.SendDate) && !string.IsNullOrEmpty(request.SendTime))
                        {
                            scheduledAt = DateTime.Parse($"{request.SendDate}T{request.SendTime}");
                        }
                        string status = request.Type == "schedule" ? "Scheduled" : "Draft";
                        
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
                        }

                        // Step 3: Re-insert channels
                        foreach (var channel in request.Channels)
                        {
                            await _communicationDao.InsertCommunicationChannelAsync(communicationId, channel, request, connection, transaction);
                        }

                        // Step 4: Re-insert recipients
                        await _communicationDao.InsertCommunicationRecipientsAsync(communicationId, request.Recipients, connection, transaction);

                        await transaction.CommitAsync();

                        // Re-schedule job if needed
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
                        throw new Exception("Transaction failed. Rolling back changes.", ex);
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
    }
}