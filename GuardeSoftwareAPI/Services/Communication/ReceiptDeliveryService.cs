using System.Text;
using System.Text.Json;
using GuardeSoftwareAPI.Dtos.Communication;
using MailKit.Net.Smtp;
using MimeKit;

namespace GuardeSoftwareAPI.Services.communication
{
    public class ReceiptDeliveryService : IReceiptDeliveryService
    {
        private const long MaximumReceiptSize = 10 * 1024 * 1024;
        private readonly IConfiguration _configuration;
        private readonly IReceiptSmtpConfigurationProvider _smtpConfigurationProvider;
        private readonly ILogger<ReceiptDeliveryService> _logger;

        public ReceiptDeliveryService(
            IConfiguration configuration,
            IReceiptSmtpConfigurationProvider smtpConfigurationProvider,
            ILogger<ReceiptDeliveryService> logger)
        {
            _configuration = configuration;
            _smtpConfigurationProvider = smtpConfigurationProvider;
            _logger = logger;
        }

        public async Task<ReceiptDeliveryResult> SendAsync(
            ReceiptDeliveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);

            await using var stream = new MemoryStream();
            await request.Receipt!.CopyToAsync(stream, cancellationToken);
            byte[] receiptBytes = stream.ToArray();
            if (receiptBytes.Length < 4 || Encoding.ASCII.GetString(receiptBytes, 0, 4) != "%PDF")
            {
                throw new ArgumentException("El archivo generado no es un PDF válido.");
            }

            string fileName = BuildFileName(request.ReceiptPeriod, request.ClientName);
            var result = new ReceiptDeliveryResult { FileName = fileName };

            var emails = (request.Emails ?? [])
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var phones = (request.WhatsAppPhones ?? [])
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await SendEmailsAsync(emails, request, fileName, receiptBytes, result, cancellationToken);
            await SendWhatsAppsAsync(phones, request, fileName, receiptBytes, result, cancellationToken);

            result.SuccessfulCount = result.Attempts.Count(attempt => attempt.Success);
            result.FailedCount = result.Attempts.Count - result.SuccessfulCount;
            return result;
        }

        private static void ValidateRequest(ReceiptDeliveryRequest request)
        {
            if (request.Receipt is null || request.Receipt.Length == 0)
                throw new ArgumentException("Falta el comprobante PDF.");
            if (request.Receipt.Length > MaximumReceiptSize)
                throw new ArgumentException("El comprobante supera el límite de 10 MB.");
            if (string.IsNullOrWhiteSpace(request.ClientName))
                throw new ArgumentException("Falta el nombre del cliente.");
            if (string.IsNullOrWhiteSpace(request.ReceiptPeriod))
                throw new ArgumentException("Falta el período del comprobante.");
            if ((request.Emails?.Count ?? 0) == 0 && (request.WhatsAppPhones?.Count ?? 0) == 0)
                throw new ArgumentException("Seleccioná al menos un destinatario.");
        }

        private async Task SendEmailsAsync(
            List<string> emails,
            ReceiptDeliveryRequest request,
            string fileName,
            byte[] receiptBytes,
            ReceiptDeliveryResult result,
            CancellationToken cancellationToken)
        {
            if (emails.Count == 0) return;
            string senderName = _configuration["SmtpSettings:SenderName"] ?? "Guarde Lo Que Quiera";

            var validEmails = new List<(string Value, MailboxAddress Address)>();
            foreach (string email in emails)
            {
                try
                {
                    validEmails.Add((email, MailboxAddress.Parse(email)));
                }
                catch
                {
                    AddAttempt(result, "email", email, false, "El email no es válido.");
                }
            }

            if (validEmails.Count == 0) return;
            var smtpCandidates = await _smtpConfigurationProvider.GetCandidatesAsync(cancellationToken);
            if (smtpCandidates.Count == 0)
            {
                foreach (var email in validEmails)
                    AddAttempt(result, "email", email.Value, false, "No hay servidores SMTP disponibles para enviar recibos.");
                return;
            }

            var pendingEmails = validEmails.ToDictionary(
                item => item.Value,
                item => item,
                StringComparer.OrdinalIgnoreCase);
            var lastErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var settings in smtpCandidates)
            {
                if (pendingEmails.Count == 0) break;
                string serverLabel = string.IsNullOrWhiteSpace(settings.Name)
                    ? $"{settings.Host}:{settings.Port}"
                    : settings.Name;

                if (string.IsNullOrWhiteSpace(settings.Host)
                    || string.IsNullOrWhiteSpace(settings.Email)
                    || string.IsNullOrWhiteSpace(settings.Password))
                {
                    foreach (string recipient in pendingEmails.Keys)
                        lastErrors[recipient] = $"{serverLabel}: configuración incompleta.";
                    continue;
                }

                using var smtp = new SmtpClient();
                try
                {
                    await smtp.ConnectAsync(settings.Host, settings.Port, settings.UseSsl, cancellationToken);
                    await smtp.AuthenticateAsync(settings.Email, settings.Password, cancellationToken);

                    foreach (var email in pendingEmails.Values.ToList())
                    {
                        try
                        {
                            var message = CreateReceiptEmail(
                                settings,
                                senderName,
                                email.Address,
                                request,
                                fileName,
                                receiptBytes);

                            await smtp.SendAsync(message, cancellationToken);
                            AddAttempt(result, "email", email.Value, true);
                            pendingEmails.Remove(email.Value);
                            lastErrors.Remove(email.Value);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "El servidor SMTP {ServerName} no pudo enviar el comprobante a {Recipient}; se probará el siguiente servidor.",
                                serverLabel,
                                email.Value);
                            lastErrors[email.Value] = $"{serverLabel}: {ex.Message}";
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "No se pudo usar el servidor SMTP {ServerName} ({Host}:{Port}); se probará el siguiente servidor.",
                        serverLabel,
                        settings.Host,
                        settings.Port);
                    foreach (string recipient in pendingEmails.Keys)
                        lastErrors[recipient] = $"{serverLabel}: {ex.Message}";
                }
                finally
                {
                    if (smtp.IsConnected)
                    {
                        try
                        {
                            await smtp.DisconnectAsync(true, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "No se pudo cerrar limpiamente la conexión SMTP del envío de recibo.");
                        }
                    }
                }
            }

            foreach (var email in pendingEmails.Values)
            {
                string error = lastErrors.TryGetValue(email.Value, out var detail)
                    ? $"No se pudo enviar con ningún servidor SMTP. Último intento: {detail}"
                    : "No se pudo enviar con ningún servidor SMTP.";
                AddAttempt(result, "email", email.Value, false, error);
            }
        }

        private static MimeMessage CreateReceiptEmail(
            SmtpSettingsModel settings,
            string senderName,
            MailboxAddress recipient,
            ReceiptDeliveryRequest request,
            string fileName,
            byte[] receiptBytes)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, settings.Email));
            message.To.Add(recipient);
            message.Subject = $"Comprobante {request.ReceiptPeriod} - {request.ClientName}";

            var body = new BodyBuilder
            {
                TextBody = $"Hola {request.ClientName},\n\nAdjuntamos tu comprobante correspondiente a {request.ReceiptPeriod}.\n\nGuarde Lo Que Quiera"
            };
            body.Attachments.Add(fileName, receiptBytes, new ContentType("application", "pdf"));
            message.Body = body.ToMessageBody();
            return message;
        }

        private async Task SendWhatsAppsAsync(
            List<string> phones,
            ReceiptDeliveryRequest request,
            string fileName,
            byte[] receiptBytes,
            ReceiptDeliveryResult result,
            CancellationToken cancellationToken)
        {
            if (phones.Count == 0) return;

            if (bool.TryParse(_configuration["WAHASettings:Enabled"], out var enabled) && !enabled)
            {
                foreach (string phone in phones)
                    AddAttempt(result, "whatsapp", phone, false, "El envío por WhatsApp está deshabilitado.");
                return;
            }

            Uri endpoint = GetWahaSendFileEndpoint();
            string session = _configuration["WAHASettings:Session"] ?? "default";
            int timeoutSeconds = int.TryParse(_configuration["WAHASettings:TimeoutSeconds"], out var timeout) && timeout > 0
                ? timeout
                : 30;
            int delayMilliseconds = int.TryParse(_configuration["WAHASettings:DelayMilliseconds"], out var delay) && delay >= 0
                ? delay
                : 3000;
            string base64Receipt = Convert.ToBase64String(receiptBytes);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            string? apiKey = _configuration["WAHASettings:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);

            for (int index = 0; index < phones.Count; index++)
            {
                string phone = phones[index];
                string? chatId = FormatPhoneForWhatsApp(phone);
                if (chatId is null)
                {
                    AddAttempt(result, "whatsapp", phone, false, "El número no es válido.");
                    continue;
                }

                var payload = new
                {
                    session,
                    chatId,
                    caption = $"Hola {request.ClientName}, adjuntamos tu comprobante de {request.ReceiptPeriod}.",
                    file = new
                    {
                        mimetype = "application/pdf",
                        filename = fileName,
                        data = base64Receipt
                    }
                };

                try
                {
                    using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    using var response = await client.PostAsync(endpoint, content, cancellationToken);
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"WAHA HTTP {(int)response.StatusCode}: {responseBody}");

                    AddAttempt(result, "whatsapp", phone, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo enviar el comprobante por WhatsApp a {Recipient}.", phone);
                    AddAttempt(result, "whatsapp", phone, false, ex.Message);
                }

                if (index < phones.Count - 1 && delayMilliseconds > 0)
                    await Task.Delay(delayMilliseconds, cancellationToken);
            }
        }

        private Uri GetWahaSendFileEndpoint()
        {
            string configuredUrl = _configuration["WAHASettings:Endpoint"]
                ?? _configuration["WAHASettings:Url"]
                ?? "http://127.0.0.1:3000/api/sendText";
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var endpoint))
                throw new InvalidOperationException("La URL de WAHA no es válida.");

            var builder = new UriBuilder(endpoint);
            string path = builder.Path.TrimEnd('/');
            if (path.EndsWith("/sendText", StringComparison.OrdinalIgnoreCase))
                path = path[..^"/sendText".Length] + "/sendFile";
            else if (!path.EndsWith("/sendFile", StringComparison.OrdinalIgnoreCase))
                path += path.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? "/sendFile" : "/api/sendFile";
            builder.Path = path;
            return builder.Uri;
        }

        private static string? FormatPhoneForWhatsApp(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            bool isInternational = phone.TrimStart().StartsWith('+');
            string clean = new(phone.Where(char.IsDigit).ToArray());
            if (clean.Length == 0) return null;

            if (!isInternational)
            {
                bool looksLikeArgentine = clean.StartsWith("549") || clean.StartsWith("54")
                    || clean.Length == 10 || (clean.Length == 11 && clean.StartsWith('0'))
                    || clean.Length == 12 || (clean.Length == 13 && clean.StartsWith('0'));
                if (!looksLikeArgentine) isInternational = true;
            }

            if (isInternational)
            {
                if (!clean.StartsWith("54")) return $"{clean}@c.us";
                clean = clean.StartsWith("549") ? clean[3..] : clean[2..];
            }
            else if (clean.StartsWith("549")) clean = clean[3..];
            else if (clean.StartsWith("54")) clean = clean[2..];

            if (clean.StartsWith('0')) clean = clean[1..];
            if (clean.Length == 10 && clean.StartsWith("15"))
                clean = "11" + clean[2..];
            else if (clean.Length == 12)
            {
                if (clean.Substring(2, 2) == "15") clean = clean.Remove(2, 2);
                else if (clean.Substring(3, 2) == "15") clean = clean.Remove(3, 2);
                else if (clean.Substring(4, 2) == "15") clean = clean.Remove(4, 2);
            }

            return clean.Length < 8 ? null : $"549{clean}@c.us";
        }

        private static string BuildFileName(string period, string clientName)
        {
            string rawName = $"Comprobante {period.Trim()} {clientName.Trim()}";
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
                rawName = rawName.Replace(invalidCharacter, ' ');
            string normalizedName = string.Join(' ', rawName.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return $"{normalizedName}.pdf";
        }

        private static void AddAttempt(
            ReceiptDeliveryResult result,
            string channel,
            string recipient,
            bool success,
            string? error = null)
        {
            result.Attempts.Add(new ReceiptDeliveryAttempt
            {
                Channel = channel,
                Recipient = recipient,
                Success = success,
                Error = error
            });
        }
    }
}
