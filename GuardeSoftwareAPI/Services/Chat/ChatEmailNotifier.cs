using GuardeSoftwareAPI.Services.communication;
using MailKit.Net.Smtp;
using MimeKit;

namespace GuardeSoftwareAPI.Services.chat
{
    public sealed class ChatEmailNotifier : IChatEmailNotifier
    {
        private const string DefaultSiteUrl = "https://www.guardeloquequiera.net/";

        private readonly IReceiptSmtpConfigurationProvider _smtpConfigurationProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatEmailNotifier> _logger;

        public ChatEmailNotifier(
            IReceiptSmtpConfigurationProvider smtpConfigurationProvider,
            IConfiguration configuration,
            ILogger<ChatEmailNotifier> logger)
        {
            _smtpConfigurationProvider = smtpConfigurationProvider;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendReplyAsync(
            string toEmail,
            string visitorName,
            string staffMessage,
            CancellationToken cancellationToken = default)
        {
            if (!MailboxAddress.TryParse(toEmail, out MailboxAddress? recipient))
                return false;

            var candidates = await _smtpConfigurationProvider.GetCandidatesAsync(cancellationToken);
            string senderName = _configuration["SmtpSettings:SenderName"] ?? "Guarde Lo Que Quiera";
            string siteUrl = _configuration["Chat:PublicSiteUrl"] ?? DefaultSiteUrl;

            foreach (var settings in candidates)
            {
                if (string.IsNullOrWhiteSpace(settings.Host)
                    || string.IsNullOrWhiteSpace(settings.Email)
                    || string.IsNullOrWhiteSpace(settings.Password))
                {
                    continue;
                }

                using var smtp = new SmtpClient { Timeout = 10_000 };
                try
                {
                    await smtp.ConnectAsync(
                        settings.Host,
                        settings.Port,
                        SmtpConnectionOptions.Resolve(settings),
                        cancellationToken);
                    await smtp.AuthenticateAsync(settings.Email, settings.Password, cancellationToken);

                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress(senderName, settings.Email));
                    message.To.Add(recipient);
                    message.Subject = "Respondimos tu consulta - Guarde Lo Que Quiera";
                    message.Body = new TextPart("plain")
                    {
                        Text = BuildBody(visitorName, staffMessage, siteUrl)
                    };

                    await smtp.SendAsync(message, cancellationToken);
                    return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "El servidor SMTP {Host}:{Port} no pudo enviar la respuesta del chat; se probará el siguiente.",
                        settings.Host,
                        settings.Port);
                }
                finally
                {
                    if (smtp.IsConnected)
                    {
                        try { await smtp.DisconnectAsync(true, CancellationToken.None); }
                        catch (Exception ex) { _logger.LogDebug(ex, "No se pudo cerrar la conexión SMTP del chat."); }
                    }
                }
            }

            return false;
        }

        public static string BuildBody(string visitorName, string staffMessage, string siteUrl)
        {
            return $"Hola {visitorName},\n\n" +
                   "Respondimos tu consulta:\n\n" +
                   $"{staffMessage}\n\n" +
                   "Para seguir la conversación, abrí el chat desde nuestra web, en el mismo navegador " +
                   $"donde nos escribiste:\n{siteUrl}\n\n" +
                   "Guarde Lo Que Quiera";
        }
    }
}
