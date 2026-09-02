using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Communication;
using Quartz;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Text;
using GuardeSoftwareAPI.Dtos.Client;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using GuardeSoftwareAPI.Hubs;
namespace GuardeSoftwareAPI.Jobs
{
    [DisallowConcurrentExecution]
    public class SendCommunicationJob : IJob
    {
        private const string AccountStatementTestPhone = "1160244908";
        private const string DefaultTestEmailAddress = "fsgbrunofranco@gmail.com";
        private static readonly Regex LegacyBrandLogoImageRegex = new(
            @"<img\b[^>]*guardeloquequiera-logo(?:\.jpg)?[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly CommunicationDao _communicationDao;
        private readonly IConfiguration _config;
        private readonly ILogger<SendCommunicationJob> _logger;
        private readonly IHubContext<CommunicationHub> _hubContext;

        public SendCommunicationJob(
            AccessDB _accessDB,
            IConfiguration config,
            ILogger<SendCommunicationJob> logger,
            IHubContext<CommunicationHub> hubContext)
        {
            _communicationDao = new CommunicationDao(_accessDB);
            _config = config;
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            int comunicadoId = context.JobDetail.JobDataMap.GetInt("CommunicationId");
            bool isTestMode = ReadBooleanJobData(context, "IsTestMode");
            string testEmail = context.JobDetail.JobDataMap.GetString("TestEmailAddress") ?? "";

            var errorLog = new StringBuilder();
            
            try
            {
                bool isMarkedAsTest = await _communicationDao.IsTestCommunicationAsync(comunicadoId);
                isTestMode = isTestMode || isMarkedAsTest;
                if (isTestMode)
                {
                    testEmail = DefaultTestEmailAddress;
                }

                _logger.LogInformation(
                    "Starting communication job for ID: {ComunicadoId}. TestMode: {IsTestMode}. MarkedAsTest: {MarkedAsTest}",
                    comunicadoId,
                    isTestMode,
                    isMarkedAsTest);

                if (isTestMode)
                {
                    _logger.LogWarning(
                        "Communication {ComunicadoId} is a test. Email will be redirected to {TestEmail} and account-statement WhatsApp will be redirected to {TestPhone}.",
                        comunicadoId,
                        testEmail,
                        AccountStatementTestPhone);
                }

                await _communicationDao.UpdateCommunicationStatusAndErrorAsync(comunicadoId, "Procesando", null);
                await _hubContext.Clients.All.SendAsync("CommunicationUpdated", comunicadoId);

                var channels = await _communicationDao.GetChannelsForSendingAsync(comunicadoId);
                bool sendToAllEmails = await _communicationDao.IsSendToAllEmailsAsync(comunicadoId);

                _logger.LogInformation("Found {ChannelCount} channels for communication {ComunicadoId}.", channels.Count, comunicadoId);

                var emailChannel = channels.FirstOrDefault(c => c.ChannelName == "Email");
                if (emailChannel != null)
                {
                    List<RecipientForSendingDto> emailRecipients;
                    if (sendToAllEmails)
                    {
                        emailRecipients = await _communicationDao.GetAllEmailRecipientsForSendingAsync(
                            comunicadoId,
                            emailChannel.CommChannelContentId);
                    }
                    else
                    {
                        emailRecipients = await _communicationDao.GetRecipientsForSendingAsync(
                            comunicadoId,
                            emailChannel.CommChannelContentId);
                        emailRecipients.AddRange(
                            await _communicationDao.GetSelectedExternalEmailRecipientsForSendingAsync(
                                comunicadoId,
                                emailChannel.CommChannelContentId));
                    }

                    if (isTestMode && emailRecipients.Count > 1)
                    {
                        emailRecipients = emailRecipients.Take(1).ToList();
                    }

                    _logger.LogInformation("Found {RecipientCount} email recipients for communication {ComunicadoId}.", emailRecipients.Count, comunicadoId);
                    await ProcessEmailChannel(emailChannel, emailRecipients, errorLog, comunicadoId, isTestMode, testEmail);
                }

                var whatsappChannel = channels.FirstOrDefault(c => c.ChannelName == "WhatsApp");
                if (whatsappChannel != null)
                {
                    if (sendToAllEmails)
                    {
                        const string unsupportedWhatsAppScope = "La selección de todos los emails sólo permite el envío por Email.";
                        _logger.LogWarning("Communication {ComunicadoId}: {Message}", comunicadoId, unsupportedWhatsAppScope);
                        errorLog.AppendLine(unsupportedWhatsAppScope);
                    }
                    else
                    {
                        bool isAccountStatement = await _communicationDao.IsAccountStatementAsync(comunicadoId);
                        if (isTestMode && !isAccountStatement)
                        {
                            _logger.LogInformation("Test communication {ComunicadoId}: WhatsApp delivery skipped to avoid sending to real client numbers.", comunicadoId);
                        }
                        else
                        {
                            var whatsappRecipients = await _communicationDao.GetRecipientsForSendingAsync(comunicadoId, whatsappChannel.CommChannelContentId);
                            _logger.LogInformation("Found {RecipientCount} WhatsApp recipients for communication {ComunicadoId}.", whatsappRecipients.Count, comunicadoId);

                            if (isTestMode && isAccountStatement)
                            {
                                foreach (var recipient in whatsappRecipients)
                                {
                                    recipient.WhatsAppPhones = [AccountStatementTestPhone];
                                    recipient.Phone = AccountStatementTestPhone;
                                }

                                _logger.LogInformation("Test account statement {ComunicadoId}: WhatsApp delivery redirected to {TestPhone}.", comunicadoId, AccountStatementTestPhone);
                            }

                            await ProcessWhatsAppChannel(whatsappChannel, whatsappRecipients, errorLog, comunicadoId);
                        }
                    }
                }

                string finalStatus = errorLog.Length > 0 ? "Finished w/ Errors" : "Finished";
                await _communicationDao.UpdateCommunicationStatusAndErrorAsync(comunicadoId, finalStatus, errorLog.Length > 0 ? errorLog.ToString() : null);
                _logger.LogInformation("Communication job for ID: {ComunicadoId} finished with status: {Status}", comunicadoId, finalStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in communication job for ID: {ComunicadoId}", comunicadoId);
                string fatalError = $"Error general en la ejecución o envío: {ex.Message}";
                if (ex.InnerException != null) fatalError += $"\n({ex.InnerException.Message})";
                await _communicationDao.UpdateCommunicationStatusAndErrorAsync(comunicadoId, "Failed", fatalError);
                throw new JobExecutionException("Job execution failed.", ex, false);
            }
            finally 
            {
                // Emitir actualización al finalizar el job independientemente de si falló o no
                await _hubContext.Clients.All.SendAsync("CommunicationUpdated", comunicadoId);
            }
        }

        private static bool ReadBooleanJobData(IJobExecutionContext context, string key)
        {
            if (!context.JobDetail.JobDataMap.TryGetValue(key, out var value) || value is null)
            {
                return false;
            }

            if (value is bool booleanValue)
            {
                return booleanValue;
            }

            return bool.TryParse(value.ToString(), out var parsedValue) && parsedValue;
        }

        private async Task ProcessEmailChannel(ChannelForSendingDto channel, List<RecipientForSendingDto> recipients, StringBuilder errorLog, int communicationId, bool isTestMode, string testEmail)
        {
            if (recipients.Count == 0)
            {
                errorLog.AppendLine("No hay emails registrados para enviar este comunicado.");
                return;
            }

            var dbSmtp = await _communicationDao.GetSmtpSettingsAsync(communicationId);
            bool isAccountStatement = await _communicationDao.IsAccountStatementAsync(communicationId);
            bool isNextMonth = await _communicationDao.IsNextMonthStatementAsync(communicationId);
            
            SmtpSettingsModel effectiveSettings;

            if (dbSmtp != null)
            {
                effectiveSettings = dbSmtp;
            }
            else
            {
                effectiveSettings = new SmtpSettingsModel
                {
                    Host = _config["SmtpSettings:Server"],
                    Port = int.Parse(_config["SmtpSettings:Port"]),
                    Email = _config["SmtpSettings:SenderEmail"],
                    Password = _config["SmtpSettings:Password"],
                    UseSsl = bool.Parse(_config["SmtpSettings:UseSsl"]),
                    EnableBcc = bool.TryParse(_config["SmtpSettings:EnableBcc"], out var bcc) && bcc,
                    BccEmail = _config["SmtpSettings:BccEmail"] ?? ""
                };
            }

            // 3. Obtener Adjuntos
            var attachments = await _communicationDao.GetAttachmentsAsync(communicationId);

            using var smtp = new SmtpClient();
            try
            {
                smtp.CheckCertificateRevocation = false;
                smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;

                await smtp.ConnectAsync(effectiveSettings.Host, effectiveSettings.Port, effectiveSettings.UseSsl);
                await smtp.AuthenticateAsync(effectiveSettings.Email, effectiveSettings.Password);

                foreach (var recipient in recipients)
                {
                    try 
                    {
                        if (isTestMode)
                        {
                            recipient.Email = string.IsNullOrWhiteSpace(testEmail)
                                ? DefaultTestEmailAddress
                                : testEmail;
                        }

                        MimeMessage message;
                        string? emailContent = null;

                        if (isAccountStatement)
                        {
                            var financialData = await _communicationDao.GetClientFinancialData(recipient.ClientId, isNextMonth);
                            
                            string dynamicHtml = GenerateAccountStatementHtml(recipient.Name, financialData, isNextMonth);
                            emailContent = dynamicHtml;
                            
                            DateTime targetDate = isNextMonth ? DateTime.Now.AddMonths(1) : DateTime.Now;
                            var tempChannel = new ChannelForSendingDto 
                            { 
                                Subject = $"Estado de Cuenta {targetDate:MM/yyyy}", 
                                Content = dynamicHtml 
                            };
                            message = CreateEmailMessage(tempChannel, recipient, effectiveSettings, attachments);
                        }
                        else 
                        {
                            string personalizedContent = RemoveLegacyBrandLogo(
                                ReplaceCommunicationPlaceholders(channel.Content, recipient.Name));
                            emailContent = personalizedContent;
                            var personalizedChannel = new ChannelForSendingDto
                            {
                                CommChannelContentId = channel.CommChannelContentId,
                                ChannelName = channel.ChannelName,
                                Subject = channel.Subject,
                                Content = personalizedContent
                            };
                            message = CreateEmailMessage(personalizedChannel, recipient, effectiveSettings, attachments);
                        }

                        string response = await smtp.SendAsync(message);
                        await LogEmailAttemptAsync(
                            channel.CommChannelContentId,
                            recipient,
                            "Exitoso",
                            response,
                            emailContent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to send email to {RecipientName} (client ID: {ClientId}, external ID: {ExternalRecipientId})",
                            recipient.Name,
                            recipient.ClientId,
                            recipient.ExternalRecipientId);
                        errorLog.AppendLine($"Email to {recipient.Email} failed: {ex.Message}");
                        await LogEmailAttemptAsync(
                            channel.CommChannelContentId,
                            recipient,
                            "Fallido",
                            ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SMTP server.");
                errorLog.AppendLine($"SMTP Connection failed: {ex.Message}");
                
                foreach (var recipient in recipients)
                {
                    await LogEmailAttemptAsync(
                        channel.CommChannelContentId,
                        recipient,
                        "Fallido",
                        "SMTP Connection Error");
                }
            }
            finally
            {
                if (smtp.IsConnected)
                    await smtp.DisconnectAsync(true);
            }
        }

        private MimeMessage CreateEmailMessage(ChannelForSendingDto channel, RecipientForSendingDto recipient, SmtpSettingsModel settings, List<AttachmentDto> attachments)
        {
            var message = new MimeMessage();
            
            message.From.Add(new MailboxAddress("Guarda Muebles - Guarde Lo Que Quiera", settings.Email));

            if (!string.IsNullOrEmpty(recipient.Email))
            {
                var addresses = recipient.Email.Split(';', StringSplitOptions.RemoveEmptyEntries);

                foreach (var address in addresses)
                {
                    try 
                    { 
                        message.To.Add(MailboxAddress.Parse(address.Trim())); 
                    }
                    catch 
                    { 
                        continue; 
                    }
                }
            }

            if (settings.EnableBcc && !string.IsNullOrWhiteSpace(settings.BccEmail))
            {
                var bccAddresses = settings.BccEmail.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var bccAddress in bccAddresses)
                {
                    try
                    {
                        message.Bcc.Add(MailboxAddress.Parse(bccAddress.Trim()));
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            message.Subject = channel.Subject;

            var builder = new BodyBuilder();
            // Aplicar los placeholders en el último punto antes de enviar el mensaje.
            // Esto cubre también los caminos de prueba o reintento que construyan el
            // ChannelForSendingDto sin pasar previamente por la personalización.
            builder.HtmlBody = RemoveLegacyBrandLogo(
                ReplaceCommunicationPlaceholders(channel.Content, recipient.Name));

            EmailTemplateInlineResources.AddReferencedResources(
                builder,
                builder.HtmlBody,
                AppContext.BaseDirectory);

            // Adjuntar archivos si existen
            if (attachments != null && attachments.Count > 0)
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

        private async Task LogEmailAttemptAsync(
            int commChannelContentId,
            RecipientForSendingDto recipient,
            string status,
            string response,
            string? sentContent = null)
        {
            if (recipient.ExternalRecipientId.HasValue)
            {
                await _communicationDao.LogMassRecipientSendAttemptAsync(
                    commChannelContentId,
                    recipient.ExternalRecipientId.Value,
                    status,
                    response,
                    sentContent);
                return;
            }

            await _communicationDao.LogSendAttemptAsync(
                commChannelContentId,
                recipient.ClientId,
                status,
                response,
                sentContent);
        }

        private string GenerateAccountStatementHtml(string clientName, ClientFinancialDto data, bool isNextMonth)
        {
            DateTime targetDate = isNextMonth ? DateTime.Now.AddMonths(1) : DateTime.Now;
            string monthYear = targetDate.ToString("MM/yyyy");
            
            string headerTag = isNextMonth ? $"<b style='color: blue;'>PROYECCIÓN PRÓXIMO MES - {monthYear}</b><br><br>" : "";

            string recargo = FormatStatementAmount(data.Surcharge);
            string saldoAnterior = FormatStatementAmount(data.PreviousBalance);
            string saldoActual = FormatStatementAmount(data.CurrentBalance);

            return $@"
            <html>
                <head></head>
                <body>
                    
                    <p><b style='color: black;'> Estimado/a: {clientName}</b></p>
                    {headerTag}
                    <p>Le recordamos que el pago de la cuota correspondiente al mes {monthYear} es hasta el día 10/{monthYear}. Vencido dicho plazo el importe mensual tendrá un recargo del 10%, sin excepción.</p>
                    
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

        private static readonly CultureInfo StatementCulture = CultureInfo.GetCultureInfo("es-AR");

        private static string FormatStatementAmount(decimal amount)
        {
            return amount.ToString("N2", StatementCulture);
        }

        private string GenerateAccountStatementWhatsAppText(
    string clientName,
    ClientFinancialDto data,
    bool isNextMonth)
{
    DateTime targetDate = isNextMonth
        ? DateTime.Now.AddMonths(1)
        : DateTime.Now;

    string monthYear = targetDate.ToString("MM/yyyy", StatementCulture);
    string dueDate = new DateTime(targetDate.Year, targetDate.Month, 10)
        .ToString("dd/MM/yyyy", StatementCulture);

    string title = isNextMonth
        ? $"Proyección de cuenta {monthYear}"
        : $"Estado de cuenta {monthYear}";

    return $"""
Hola {clientName},

{title}

Saldo anterior: $ {FormatStatementAmount(data.PreviousBalance)}
Recargo: $ {FormatStatementAmount(data.Surcharge)}
*Total a abonar: $ {FormatStatementAmount(data.CurrentBalance)}*

La Administración
WhatsApp 11-5780-0251
Lunes a viernes de 09:00 a 16:00
Sábados de 09:00 a 13:00
""";
}

        private async Task<string> SendWhatsAppViaWahaAsync(string phone, string messageText)
        {
            string wahaUrl = _config["WAHASettings:Endpoint"]
                ?? _config["WAHASettings:Url"]
                ?? "http://127.0.0.1:3000/api/sendText";
            string session = _config["WAHASettings:Session"] ?? "default";

            if (!Uri.TryCreate(wahaUrl, UriKind.Absolute, out var endpoint))
            {
                throw new InvalidOperationException("La URL de WAHA no es válida. Revisá WAHASettings:Endpoint.");
            }

            int timeoutSeconds = int.TryParse(_config["WAHASettings:TimeoutSeconds"], out var configuredTimeout)
                && configuredTimeout > 0
                ? configuredTimeout
                : 30;

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) })
            {
                string? apiKey = _config["WAHASettings:ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
                }
                
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var payload = new
                {
                    chatId = phone, 
                    text = messageText,
                    session
                };

                var json = JsonSerializer.Serialize(payload);
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                try 
                {
                    var response = await client.PostAsync(endpoint, content);
                    var responseBody = await response.Content.ReadAsStringAsync();
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"WAHA HTTP {(int)response.StatusCode} ({response.StatusCode}): {responseBody}");
                    }

                    return string.IsNullOrWhiteSpace(responseBody)
                        ? $"WAHA HTTP {(int)response.StatusCode}"
                        : responseBody;
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"Fallo de red hacia WAHA (Verifica Docker): {ex.Message}");
                }
            }
        }

        private string? FormatPhoneForWhatsApp(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            bool isInternational = phone.TrimStart().StartsWith("+");
            var clean = new string(phone.Where(char.IsDigit).ToArray());

            if (string.IsNullOrEmpty(clean)) return null;

            if (!isInternational)
            {
                // Detectar si es un número internacional que no le pusieron el '+'
                bool looksLikeArgentine = 
                    clean.StartsWith("549") || 
                    clean.StartsWith("54") ||
                    (clean.Length == 10) ||
                    (clean.Length == 11 && clean.StartsWith("0")) ||
                    (clean.Length == 12) || // Podría tener el 15
                    (clean.Length == 13 && clean.StartsWith("0"));

                if (!looksLikeArgentine)
                {
                    isInternational = true;
                }
            }

            if (isInternational)
            {
                if (!clean.StartsWith("54"))
                {
                    return $"{clean}@c.us";
                }
                
                if (clean.StartsWith("549")) clean = clean.Substring(3);
                else if (clean.StartsWith("54")) clean = clean.Substring(2);
            }
            else
            {
                if (clean.StartsWith("549")) clean = clean.Substring(3);
                else if (clean.StartsWith("54")) clean = clean.Substring(2);
            }

            // --- NORMALIZACIÓN ARGENTINA ---
            if (clean.StartsWith("0"))
            {
                clean = clean.Substring(1);
            }

            if (clean.Length == 10 && clean.StartsWith("15"))
            {
                // El usuario omitió el código de área 11 (Buenos Aires) y solo escribió el 15.
                // En Argentina, los números locales de 8 dígitos solo existen con el código de área 11.
                clean = "11" + clean.Substring(2);
            }
            else if (clean.Length == 12)
            {
                if (clean.Substring(2, 2) == "15") clean = clean.Remove(2, 2);
                else if (clean.Substring(3, 2) == "15") clean = clean.Remove(3, 2);
                else if (clean.Substring(4, 2) == "15") clean = clean.Remove(4, 2);
            }

            return $"549{clean}@c.us";
        }

        private string StripHtmlForWhatsApp(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string text = Regex.Replace(input, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<\s*li[^>]*>", "• ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"</\s*(p|div|li|h[1-6]|tr|table)\s*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<.*?>", string.Empty, RegexOptions.Singleline);

            text = System.Net.WebUtility.HtmlDecode(text);
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = Regex.Replace(text, @"[ \t]+\n", "\n");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return string.Join("\n", text
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0))
                .Trim();
        }

        private static string ReplaceCommunicationPlaceholders(string input, string clientName)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string encodedClientName = System.Net.WebUtility.HtmlEncode(clientName ?? string.Empty);

            return input
                .Replace("{data[0]}", encodedClientName, StringComparison.OrdinalIgnoreCase)
                .Replace("{{clientName}}", encodedClientName, StringComparison.OrdinalIgnoreCase)
                .Replace("{clientName}", encodedClientName, StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveLegacyBrandLogo(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Evita que un comunicado antiguo guardado en la base vuelva a enviar
            // el logo aunque todavía conserve una URL HTTP o una referencia CID.
            return LegacyBrandLogoImageRegex.Replace(input, string.Empty);
        }

        private async Task ProcessWhatsAppChannel(ChannelForSendingDto channel, List<RecipientForSendingDto> recipients, StringBuilder errorLog, int communicationId)
        {
            bool isAccountStatement = await _communicationDao.IsAccountStatementAsync(communicationId);

            if (bool.TryParse(_config["WAHASettings:Enabled"], out var enabled) && !enabled)
            {
                const string disabledMessage = "El envío por WhatsApp está deshabilitado en la configuración.";
                errorLog.AppendLine(disabledMessage);
                foreach (var recipient in recipients)
                {
                    await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", disabledMessage);
                }
                return;
            }

            bool isNextMonth = isAccountStatement && await _communicationDao.IsNextMonthStatementAsync(communicationId);
            int delayMilliseconds = int.TryParse(_config["WAHASettings:DelayMilliseconds"], out var configuredDelay)
                && configuredDelay >= 0
                ? configuredDelay
                : 3000;

            foreach (var recipient in recipients)
            {
                var phoneNumbers = new List<string>();
                if (isAccountStatement)
                {
                    phoneNumbers.AddRange(recipient.WhatsAppPhones);
                }
                else if (!string.IsNullOrWhiteSpace(recipient.Phone))
                {
                    phoneNumbers.Add(recipient.Phone!);
                }

                if (phoneNumbers.Count == 0)
                {
                    const string noPhoneMessage = "Sin número de WhatsApp habilitado";
                    await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", noPhoneMessage);
                    errorLog.AppendLine($"WhatsApp para {recipient.Name}: {noPhoneMessage}");
                    continue;
                }

                string messageToSend;
                try
                {
                    if (isAccountStatement)
                    {
                        var financialData = await _communicationDao.GetClientFinancialData(recipient.ClientId, isNextMonth);
                        messageToSend = GenerateAccountStatementWhatsAppText(recipient.Name, financialData, isNextMonth);
                    }
                    else
                    {
                        string personalizedContent = ReplaceCommunicationPlaceholders(channel.Content, recipient.Name);
                        messageToSend = StripHtmlForWhatsApp(personalizedContent);
                    }

                    if (string.IsNullOrWhiteSpace(messageToSend))
                    {
                        const string emptyMessage = "El contenido de WhatsApp está vacío";
                        await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", emptyMessage);
                        errorLog.AppendLine($"WhatsApp para {recipient.Name}: {emptyMessage}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    await _communicationDao.LogSendAttemptAsync(channel.CommChannelContentId, recipient.ClientId, "Fallido", ex.Message);
                    errorLog.AppendLine($"Error generando WhatsApp para {recipient.Name}: {ex.Message}");
                    continue;
                }

                var sentPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var phone in phoneNumbers)
                {
                    string rawPhone = phone.Trim();
                    string? formattedPhone = FormatPhoneForWhatsApp(rawPhone);

                    if (string.IsNullOrWhiteSpace(formattedPhone))
                    {
                        const string invalidPhoneMessage = "El número de WhatsApp no es válido";
                        await _communicationDao.LogSendAttemptAsync(
                            channel.CommChannelContentId,
                            recipient.ClientId,
                            "Fallido",
                            invalidPhoneMessage,
                            recipientPhone: rawPhone);
                        errorLog.AppendLine($"WhatsApp para {recipient.Name} ({rawPhone}): {invalidPhoneMessage}");
                        continue;
                    }

                    if (!sentPhones.Add(formattedPhone))
                    {
                        continue;
                    }

                    try
                    {
                        string providerResponse = await SendWhatsAppViaWahaAsync(formattedPhone, messageToSend);
                        await _communicationDao.LogSendAttemptAsync(
                            channel.CommChannelContentId,
                            recipient.ClientId,
                            "Exitoso",
                            providerResponse,
                            messageToSend,
                            rawPhone);

                        if (delayMilliseconds > 0)
                        {
                            await Task.Delay(delayMilliseconds);
                        }
                    }
                    catch (Exception ex)
                    {
                        await _communicationDao.LogSendAttemptAsync(
                            channel.CommChannelContentId,
                            recipient.ClientId,
                            "Fallido",
                            ex.Message,
                            recipientPhone: rawPhone);
                        errorLog.AppendLine($"Error WAHA para {recipient.Name} ({rawPhone}): {ex.Message}");
                    }
                }
            }
        }
    }
}
