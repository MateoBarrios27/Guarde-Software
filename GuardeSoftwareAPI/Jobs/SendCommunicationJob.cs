using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Communication;
using Quartz;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Text;

namespace GuardeSoftwareAPI.Jobs
{
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
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, "Procesando");

                var channels = await _communicationDao.GetChannelsForSendingAsync(comunicadoId);
                var recipients = await _communicationDao.GetRecipientsForSendingAsync(comunicadoId);

                _logger.LogInformation("Found {RecipientCount} recipients and {ChannelCount} channels.", recipients.Count, channels.Count);

                var emailChannel = channels.FirstOrDefault(c => c.ChannelName == "Email");
                if (emailChannel != null)
                {
                    await ProcessEmailChannel(emailChannel, recipients, errorLog, comunicadoId);
                }

                var whatsappChannel = channels.FirstOrDefault(c => c.ChannelName == "WhatsApp");
                if (whatsappChannel != null)
                {
                    await ProcessWhatsAppChannel(whatsappChannel, recipients, errorLog);
                }

                string finalStatus = errorLog.Length > 0 ? "Finished w/ Errors" : "Finished";
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, finalStatus);
                _logger.LogInformation("Communication job for ID: {ComunicadoId} finished with status: {Status}", comunicadoId, finalStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in communication job for ID: {ComunicadoId}", comunicadoId);
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, "Failed");
                throw new JobExecutionException("Job execution failed.", ex, false);
            }
        }

        private async Task ProcessEmailChannel(ChannelForSendingDto channel, List<RecipientForSendingDto> recipients, StringBuilder errorLog, int communicationId)
        {
            // 1. Obtener Config SMTP de la BD
            var dbSmtp = await _communicationDao.GetSmtpSettingsAsync(communicationId);
            
            // 2. CONSTRUIR OBJETO DE CONFIGURACIÓN "EFECTIVO"
            // Si dbSmtp es null, creamos uno manualmente con los datos de appsettings.
            // Esto evita que pasemos un objeto nulo a CreateEmailMessage.
            SmtpSettingsModel effectiveSettings;

            if (dbSmtp != null)
            {
                effectiveSettings = dbSmtp;
            }
            else
            {
                // Fallback a appsettings.json
                effectiveSettings = new SmtpSettingsModel
                {
                    Host = _config["SmtpSettings:Server"],
                    Port = int.Parse(_config["SmtpSettings:Port"]),
                    Email = _config["SmtpSettings:SenderEmail"],
                    Password = _config["SmtpSettings:Password"],
                    UseSsl = bool.Parse(_config["SmtpSettings:UseSsl"]),
                    // Intentamos leer configuración de BCC si existe en appsettings, sino defaults
                    EnableBcc = bool.TryParse(_config["SmtpSettings:EnableBcc"], out var bcc) && bcc,
                    BccEmail = _config["SmtpSettings:BccEmail"] ?? ""
                };
            }

            // 3. Obtener Adjuntos
            var attachments = await _communicationDao.GetAttachmentsAsync(communicationId);

            using var smtp = new SmtpClient();
            try
            {
                // Ignorar validación de certificado (útil para hosts compartidos)
                smtp.CheckCertificateRevocation = false;
                smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;

                // Conectar usando el objeto "effectiveSettings" que garantizamos NO es nulo
                await smtp.ConnectAsync(effectiveSettings.Host, effectiveSettings.Port, effectiveSettings.UseSsl);
                await smtp.AuthenticateAsync(effectiveSettings.Email, effectiveSettings.Password);

                foreach (var recipient in recipients)
                {
                    try
                    {
                        // AQUÍ ESTABA EL ERROR: Antes pasabas 'dbSmtp' (que era null).
                        // Ahora pasamos 'effectiveSettings' que tiene los datos de appsettings cargados.
                        var message = CreateEmailMessage(channel, recipient, effectiveSettings, attachments); 
                        
                        string response = await smtp.SendAsync(message);
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Exitoso", response);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send email to client ID: {ClientId}", recipient.ClientId);
                        errorLog.AppendLine($"Email to {recipient.Email} failed: {ex.Message}");
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SMTP server.");
                errorLog.AppendLine($"SMTP Connection failed: {ex.Message}");
                
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

        private async Task ProcessWhatsAppChannel(ChannelForSendingDto channel, List<RecipientForSendingDto> recipients, StringBuilder errorLog)
        {
            _logger.LogInformation("Processing WhatsApp channel (placeholder)...");
            
            foreach (var recipient in recipients)
            {
                if (string.IsNullOrEmpty(recipient.Phone))
                {
                    await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "Client has no phone number");
                    continue;
                }
                
                // Placeholder logic...
                await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "WhatsApp sending not implemented");
            }
        }

        private MimeMessage CreateEmailMessage(ChannelForSendingDto channel, RecipientForSendingDto recipient, SmtpSettingsModel smtpSettings, List<AttachmentDto> attachments)
        {
            var message = new MimeMessage();
            
            message.From.Add(new MailboxAddress("Guarde lo que quiera - Abono", smtpSettings.Email));
            
            message.To.Add(new MailboxAddress(recipient.Name, recipient.Email));
            
            if (smtpSettings.EnableBcc && !string.IsNullOrEmpty(smtpSettings.BccEmail))
            {
                message.Bcc.Add(new MailboxAddress("Copia comunicado", smtpSettings.BccEmail));
            }
            
            message.Subject = channel.Subject;

            var builder = new BodyBuilder { HtmlBody = channel.Content };

            if (attachments != null)
            {
                foreach (var att in attachments)
                {
                    if (File.Exists(att.FilePath))
                    {
                        builder.Attachments.Add(att.FilePath);
                    }
                }
            }

            message.Body = builder.ToMessageBody();
            return message;
        }
    }
}