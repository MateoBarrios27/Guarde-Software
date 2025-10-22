using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Communication;
using Quartz;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Text;


namespace GuardeSoftwareAPI.Jobs
{
    // Prevents the same job (for the same ID) from running concurrently
    [DisallowConcurrentExecution]
    public class SendCommunicationJob : IJob
    {
        private readonly CommunicationDao _communicationDao;
        private readonly IConfiguration _config;
        private readonly ILogger<SendCommunicationJob> _logger;

        public SendCommunicationJob(
            AccessDB _accessDB,
            IConfiguration config,
            ILogger<SendCommunicationJob> logger)
        {
            _communicationDao = new CommunicationDao(_accessDB);
            _config = config;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            int comunicadoId = context.JobDetail.JobDataMap.GetInt("CommunicationId");
            _logger.LogInformation("Starting communication job for ID: {ComunicadoId}", comunicadoId);

            var errorLog = new StringBuilder();
            
            try
            {
                // Step 1: Set status to 'Procesando'
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, "Procesando");

                // Step 2: Get all data needed for the send
                var channels = await _communicationDao.GetChannelsForSendingAsync(comunicadoId);
                var recipients = await _communicationDao.GetRecipientsForSendingAsync(comunicadoId);

                _logger.LogInformation("Found {RecipientCount} recipients and {ChannelCount} channels.", recipients.Count, channels.Count);

                // Step 3: Send via Email (if configured)
                var emailChannel = channels.FirstOrDefault(c => c.ChannelName == "Email");
                if (emailChannel != null)
                {
                    await ProcessEmailChannel(emailChannel, recipients, errorLog);
                }

                // Step 4: Send via WhatsApp (if configured)
                var whatsappChannel = channels.FirstOrDefault(c => c.ChannelName == "WhatsApp");
                if (whatsappChannel != null)
                {
                    await ProcessWhatsAppChannel(whatsappChannel, recipients, errorLog);
                }

                // Step 5: Set final status
                string finalStatus = errorLog.Length > 0 ? "Finished w/ Errors" : "Finished";
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, finalStatus);
                _logger.LogInformation("Communication job for ID: {ComunicadoId} finished with status: {Status}", comunicadoId, finalStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in communication job for ID: {ComunicadoId}", comunicadoId);
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, "Failed");
                
                // Rethrow to let Quartz know the job failed
                throw new JobExecutionException("Job execution failed.", ex, false);
            }
        }

        /// <summary>
        /// Connects to SMTP and sends all emails.
        /// </summary>
        private async Task ProcessEmailChannel(ChannelForSendingDto channel, List<RecipientForSendingDto> recipients, StringBuilder errorLog)
        {
            var smtpSettings = _config.GetSection("SmtpSettings");
            using var smtp = new SmtpClient();

            try
            {
                smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                // Connect once
                await smtp.ConnectAsync(
                    smtpSettings["Server"],
                    int.Parse(smtpSettings["Port"]!),
                    bool.Parse(smtpSettings["UseSsl"]!)
                );
                await smtp.AuthenticateAsync(smtpSettings["SenderEmail"], smtpSettings["Password"]);

                // Loop and send
                foreach (var recipient in recipients)
                {
                    if (string.IsNullOrEmpty(recipient.Email))
                    {
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "Client has no email address");
                        continue;
                    }

                    try
                    {
                        var message = CreateEmailMessage(channel, recipient, smtpSettings);
                        string response = await smtp.SendAsync(message);
                        
                        // Log success
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Exitoso", response);
                    }
                    catch (Exception ex)
                    {
                        // Log individual send failure
                        _logger.LogWarning(ex, "Failed to send email to client ID: {ClientId}", recipient.ClientId);
                        errorLog.AppendLine($"Email to {recipient.Email} failed: {ex.Message}");
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log SMTP connection failure
                _logger.LogError(ex, "Failed to connect to SMTP server.");
                errorLog.AppendLine($"SMTP Connection failed: {ex.Message}");
                // Log a failure for all recipients of this channel
                foreach (var recipient in recipients)
                {
                    await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "SMTP Connection Error");
                }
            }
            finally
            {
                if (smtp.IsConnected)
                    await smtp.DisconnectAsync(true);
            }
        }

        /// <summary>
        /// Placeholder for WhatsApp API integration.
        /// </summary>
        private async Task ProcessWhatsAppChannel(ChannelForSendingDto channel, List<RecipientForSendingDto> recipients, StringBuilder errorLog)
        {
            _logger.LogInformation("Processing WhatsApp channel (placeholder)...");
            // Here you would integrate with a service like Twilio or Meta's API

            foreach (var recipient in recipients)
            {
                if (string.IsNullOrEmpty(recipient.Phone))
                {
                    await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "Client has no phone number");
                    continue;
                }
                
                // --- PSEUDO-CODE ---
                // try
                // {
                //    var whatsAppApi = new WhatsAppApiClient(...);
                //    var response = await whatsAppApi.SendMessageAsync(recipient.Phone, channel.Content);
                //    await _communicationDao.LogSendAttemptAsync(channel.IdComunicadoCanal, recipient.ClientId, "Exitoso", response.MessageId);
                // }
                // catch(Exception ex)
                // {
                //    errorLog.AppendLine($"WhatsApp to {recipient.Phone} failed: {ex.Message}");
                //    await _communicationDao.LogSendAttemptAsync(channel.IdComunicadoCanal, recipient.ClientId, "Fallido", ex.Message);
                // }

                // For now, log as "Not Implemented"
                await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "WhatsApp sending not implemented");
            }
        }

        /// <summary>
        /// Helper to build the MimeMessage for MailKit.
        /// </summary>
        private MimeMessage CreateEmailMessage(ChannelForSendingDto channel, RecipientForSendingDto recipient, IConfigurationSection settings)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings["SenderName"], settings["SenderEmail"]));
            message.To.Add(new MailboxAddress(recipient.Name, recipient.Email));
            message.Subject = channel.Subject ?? "Notification from GuardeSoftware";

            // TODO: Replace placeholders in content
            // var personalizedContent = channel.Content.Replace("{{NombreCliente}}", recipient.Name);
            var personalizedContent = channel.Content; // Using raw content for now

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = personalizedContent
            };
            message.Body = bodyBuilder.ToMessageBody();
            
            return message;
        }
    }
}