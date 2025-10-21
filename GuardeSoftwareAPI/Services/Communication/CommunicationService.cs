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

        public CommunicationService(
            AccessDB _accessDB, // Inject your AccessDB
            ISchedulerFactory schedulerFactory)
        {
            _communicationDao = new CommunicationDao(_accessDB);
            accessDB = _accessDB;
            _schedulerFactory = schedulerFactory;
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
                            await _communicationDao.InsertCommunicationChannelContentAsync(newId, channel, request, connection, transaction);
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
    }
}