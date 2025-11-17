using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Communication;
using GuardeSoftwareAPI.Services.communication; // Para IFileStorageService
using Quartz;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Text;
using System.Text.Json;

namespace GuardeSoftwareAPI.Jobs
{
    [DisallowConcurrentExecution]
    public class SendCommunicationJob : IJob
    {
        private readonly CommunicationDao _communicationDao;
        private readonly IFileStorageService _fileStorageService; // --- AÑADIDO ---
        private readonly IConfiguration _config;
        private readonly ILogger<SendCommunicationJob> _logger;

        public SendCommunicationJob(
            AccessDB accessDB,
            IConfiguration config,
            ILogger<SendCommunicationJob> logger,
            IFileStorageService fileStorageService // --- AÑADIDO ---
        )
        {
            _communicationDao = new CommunicationDao(accessDB);
            _config = config;
            _logger = logger;
            _fileStorageService = fileStorageService; // --- AÑADIDO ---
        }

        public async Task Execute(IJobExecutionContext context)
        {
            // --- LEER NUEVOS DATOS DEL JOB ---
            int comunicadoId = context.JobDetail.JobDataMap.GetInt("CommunicationId");
            string mailServerId = context.JobDetail.JobDataMap.GetString("MailServerId") ?? "default";
            bool isRetry = context.JobDetail.JobDataMap.GetBoolean("IsRetry");
            
            _logger.LogInformation("Starting communication job for ID: {ComunicadoId} (Server: {Server}, IsRetry: {Retry})", 
                comunicadoId, mailServerId, isRetry);

            var errorLog = new StringBuilder();
            List<AttachmentDto> attachmentsToCleanup = new List<AttachmentDto>();

            try
            {
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, "Processing");

                var channels = await _communicationDao.GetChannelsForSendingAsync(comunicadoId);
                // --- ACTUALIZADO: Pasa el flag de reintento ---
                var recipients = await _communicationDao.GetRecipientsForSendingAsync(comunicadoId, isRetry);

                _logger.LogInformation("Found {RecipientCount} recipients and {ChannelCount} channels.", recipients.Count, channels.Count);

                var emailChannel = channels.FirstOrDefault(c => c.ChannelName == "Email");
                if (emailChannel != null)
                {
                    // Deserializa los adjuntos para pasarlos al procesador
                    attachmentsToCleanup = JsonSerializer.Deserialize<List<AttachmentDto>>(emailChannel.AttachmentsJson ?? "[]", 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AttachmentDto>();
                        
                    await ProcessEmailChannel(emailChannel, recipients, errorLog, mailServerId, attachmentsToCleanup);
                }

                var whatsappChannel = channels.FirstOrDefault(c => c.ChannelName == "WhatsApp");
                if (whatsappChannel != null)
                {
                    await ProcessWhatsAppChannel(whatsappChannel, recipients, errorLog);
                }

                string finalStatus = errorLog.Length > 0 ? "Finished w/ Errors" : "Finished";
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, finalStatus);
                _logger.LogInformation("Communication job for ID: {ComunicadoId} finished with status: {Status}", comunicadoId, finalStatus);

                // --- NUEVO: Limpieza de archivos del VPS ---
                // Si el trabajo fue exitoso (sin errores) Y NO fue un reintento (fue el envío final)
                // Y hay adjuntos para limpiar...
                if (finalStatus == "Finished" && !isRetry && attachmentsToCleanup.Count > 0)
                {
                    _logger.LogInformation("Send successful. Deleting {Count} temporary attachments from VPS for job ID: {ComunicadoId}", attachmentsToCleanup.Count, comunicadoId);
                    var urlsToDelete = attachmentsToCleanup.Select(a => a.FileUrl).ToList();
                    await _fileStorageService.DeleteFilesAsync(urlsToDelete);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in communication job for ID: {ComunicadoId}", comunicadoId);
                await _communicationDao.UpdateCommunicationStatusAsync(comunicadoId, "Failed");
                throw new JobExecutionException("Job execution failed.", ex, false);
            }
        }

        private async Task ProcessEmailChannel(
            ChannelForSendingDto channel, 
            List<RecipientForSendingDto> recipients, 
            StringBuilder errorLog, 
            string mailServerId,
            List<AttachmentDto> attachments)
        {
            // --- ACTUALIZADO: Carga la configuración del servidor de mail dinámicamente ---
            // Asume que tu appsettings.json tiene:
            // "SmtpSettings": { "default": { ... }, "backup-01": { ... } }
            var smtpSettings = _config.GetSection($"SmtpSettings:{mailServerId}");
            if (!smtpSettings.Exists())
            {
                _logger.LogError("Mail server config '{ServerId}' not found. Falling back to 'default'.", mailServerId);
                smtpSettings = _config.GetSection("SmtpSettings:default");
            }

            using var smtp = new SmtpClient();

            try
            {
                smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                await smtp.ConnectAsync(
                    smtpSettings["Server"],
                    int.Parse(smtpSettings["Port"]!),
                    bool.Parse(smtpSettings["UseSsl"]!)
                );
                await smtp.AuthenticateAsync(smtpSettings["SenderEmail"], smtpSettings["Password"]);

                foreach (var recipient in recipients)
                {
                    if (string.IsNullOrEmpty(recipient.Email))
                    {
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "Client has no email address");
                        continue;
                    }

                    try
                    {
                        var message = await CreateEmailMessageAsync(channel, recipient, smtpSettings, attachments);
                        string response = await smtp.SendAsync(message);
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Exitoso", response);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send email to client ID: {ClientId}", recipient.ClientId);
                        errorLog.AppendLine($"Email to {recipient.Email} failed: {ex.Message}");
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", ex.Message);
                        
                        // --- CONTROL DE LÍMITE DE SERVIDOR ---
                        // Si el error es por "límite de envíos" (depende de la respuesta de tu SMTP),
                        // rompe el bucle para no seguir intentando.
                        if (ex.Message.Contains("rate limit") || ex.Message.Contains("limit exceeded"))
                        {
                            _logger.LogError(ex, "Mail server limit reached. Stopping job for ID: {CommId}", channel.CommChannelContentId);
                            errorLog.AppendLine("Mail server limit reached. Remaining recipients set to 'Pending'.");
                            // (Opcional: marcar los restantes como 'Pendiente' en la DB)
                            break; 
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SMTP server: {Server}", smtpSettings["Server"]);
                errorLog.AppendLine($"SMTP Connection failed: {ex.Message}");
                foreach (var recipient in recipients)
                {
                    await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "SMTP Connection Error");
                }
            }
            finally
            {
                if (smtp.IsConnected) await smtp.DisconnectAsync(true);
            }
        }
        
        // --- MÉTODO ACTUALIZADO: Ahora es Async y maneja descarga de adjuntos ---
        private async Task<MimeMessage> CreateEmailMessageAsync(
            ChannelForSendingDto channel, 
            RecipientForSendingDto recipient, 
            IConfigurationSection settings,
            List<AttachmentDto> attachments)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings["SenderName"], settings["SenderEmail"]));
            message.To.Add(new MailboxAddress(recipient.Name, recipient.Email));
            message.Subject = channel.Subject ?? "Notification from GuardeSoftware";

            var personalizedContent = channel.Content; // TODO: Reemplazar placeholders
            var bodyBuilder = new BodyBuilder { HtmlBody = personalizedContent };

            // --- LÓGICA DE ADJUNTOS ---
            if (attachments.Count > 0)
            {
                // Necesitas HttpClient para descargar los archivos de tu propio VPS
                // Es mejor inyectar IHttpClientFactory, pero esto funciona para el ejemplo
                using (var client = new HttpClient())
                {
                    foreach(var att in attachments)
                    {
                        try
                        {
                            // Descarga el archivo desde la URL pública de tu VPS
                            byte[] fileBytes = await client.GetByteArrayAsync(att.FileUrl);
                            // Lo adjunta al email
                            bodyBuilder.Attachments.Add(att.FileName, fileBytes);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to download or attach file {FileName} from {FileUrl}", att.FileName, att.FileUrl);
                            // Opcional: registrar este error específico
                        }
                    }
                }
            }
            // --- FIN LÓGICA DE ADJUNTOS ---

            message.Body = bodyBuilder.ToMessageBody();
            return message;
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
                
                // (Lógica de API de WhatsApp... por ahora marca como fallido)
                await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", "WhatsApp sending not implemented");
            }
        }
    }
}