using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Communication;
using Quartz;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Text;
using GuardeSoftwareAPI.Dtos.Client;

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

            bool isAccountStatement = await _communicationDao.IsAccountStatementAsync(communicationId);
            
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
                        MimeMessage message;

                        if (isAccountStatement)
                        {
                            // LÓGICA ESPECIAL: Generar HTML dinámico
                            var financialData = await _communicationDao.GetClientFinancialData(recipient.ClientId); // Ver paso 4
                            string dynamicHtml = GenerateAccountStatementHtml(recipient.Name, financialData);
                            
                            // Creamos un canal temporal con el HTML generado
                            var tempChannel = new ChannelForSendingDto 
                            { 
                                Subject = $"Estado de Cuenta {DateTime.Now:MM/yyyy}", 
                                Content = dynamicHtml 
                            };
                            message = CreateEmailMessage(tempChannel, recipient, effectiveSettings, attachments);
                        }
                        else 
                        {
                            // Lógica normal
                            message = CreateEmailMessage(channel, recipient, effectiveSettings, attachments);
                        }

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

        private string GenerateAccountStatementHtml(string clientName, ClientFinancialDto data)
        {
            // Variables de tiempo (igual que time.strftime("%m/%Y"))
            string monthYear = DateTime.Now.ToString("MM/yyyy");
            
            // Formato de moneda (N2 para 2 decimales, igual que el CSV original)
            string recargo = data.Surcharge.ToString("N2");
            string saldoAnterior = data.PreviousBalance.ToString("N2");
            string saldoActual = data.CurrentBalance.ToString("N2");

            return $@"
            <html>
                <head></head>
                <body>
                    <p><b style='color: black;'> Estimado/a: {clientName}</b></p> 
                    
                    <p>Le recordamos que el vencimiento de la cuota correspondiente al mes {monthYear} es el 10/{monthYear}. Vencido dicho plazo el importe mensual tendrá un recargo del 10%, sin excepción.</p>
                    
                    <b style='color: green;'> ""No pierda su beneficio por pago puntual"", por atrasos reiterados su abono será ajustado a los valores actuales""</b></p>

                    <p style='color: red;'>Para tener acceso al espacio alquilado deberá tener el pago mensual al día.</p>
                    
                    <table border='1'>
                    <tr>
                        <td><b style='color: black;'>Estado de Cuenta</b></td>
                        <td><b style='color: black;'>Monto</b></td>
                    </tr>
                    <tr>
                        <td><b style='color: black;'> Recargo fuera de termino</b></p></td>
                        <td> $ {recargo} </td>
                    </tr>
                    <tr>
                        <td><b style='color: black;'> Saldo  Anterior</b></p></td>
                        <td> $ {saldoAnterior} </td>
                    </tr>
                    <tr>
                        <td><b style='color: black;'>Saldo Actual</b></p></td>
                        <td><b> $ {saldoActual} </b></td>
                    </tr>
                    <tr>
                    </tr>
                    </table>

                    <p><b style='color: green;'>Los aumentos en los abonos se verán reflejados en su Estado de Cuenta cada 4 meses, esto significa que abonará 3 meses con el mismo importe y en el cuarto mes verá reflejado un aumento según el valor de mercado.</p>

                    <p><b style='color: red;'>El último día de pago es el 10 sin excepciones de feriados, domingos, etc.</p>

                    <p><b style='color: blue;'>Adicionalmente vera que el importe tendrá centavos que corresponden a la identificación de cada cliente, por ejemplo $ 85491,40 el 1,40 va a estar asociado a su cuenta y de fácil identificación ya que a veces es complicado identificar cada pago y asociarlo rápidamente a su saldo.</p>

                    <b style='color: blue;'><p>Forma de Pago:</p></b>
                    <b style='color: blue;'><p>Según lo acordado con ustedes en el Contrato de Locación</p></b>

                    <b style='color: blue;'><p></b> <b>En Nuestras Instalaciones: Francisco  Borges 4280 Munro (Vte. López)</b>
                    <p><b style='color: black;'>De lunes a viernes de 09 a 16 hs, administración hasta las 15 y 30 hs. y sábados de 09 a 13 hs, administración hasta las 12 y 30 hs.</p>
                    <p><b style='color: black;'>TEL.: 011-4730-2192 / 011-4762-0599 / WhatsApp 11-5780-0251</p>
                    
                    <b style='color: blue;'><p></p>
                    <p></p>
                    <b style='color: gray;'><p>Saludos</p></b>
                    <b style='color: gray;'><p>La Administración</p></b>
                    <p><a href='https://www.guardeloquequiera.com.ar/'>guardeloquequiera.com.ar</a></p>
                    <p><b style='color: gray;'>011-4762-0599 / 011-4730-2192</p>
                    <p><b style='color: green;'>WhatsApp 115-780-0251</p>

                    <b style='color: green;'><p></p>
                </body>
            </html>";
        }
    }
}