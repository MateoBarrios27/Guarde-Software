using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Services.communication
{
    public class ReceiptSmtpConfigurationProvider : IReceiptSmtpConfigurationProvider
    {
        private readonly CommunicationDao _communicationDao;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReceiptSmtpConfigurationProvider> _logger;

        public ReceiptSmtpConfigurationProvider(
            AccessDB accessDB,
            IConfiguration configuration,
            ILogger<ReceiptSmtpConfigurationProvider> logger)
        {
            _communicationDao = new CommunicationDao(accessDB);
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IReadOnlyList<SmtpSettingsModel>> GetCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = new List<SmtpSettingsModel>();

            try
            {
                var storedConfigurations = await _communicationDao.GetAllSmtpConfigsAsync();
                candidates.AddRange(storedConfigurations
                    .OrderByDescending(configuration => configuration.IsReceiptDefault)
                    .ThenBy(configuration => configuration.Id)
                    .Select(configuration => new SmtpSettingsModel
                    {
                        Id = configuration.Id,
                        Name = configuration.Name,
                        Host = configuration.Host,
                        Port = configuration.Port,
                        Email = configuration.Email,
                        Password = configuration.Password,
                        UseSsl = configuration.UseSsl,
                        EnableBcc = configuration.EnableBcc,
                        BccEmail = configuration.BccEmail,
                        IsReceiptDefault = configuration.IsReceiptDefault
                    }));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudieron consultar las configuraciones SMTP guardadas; se intentará usar la configuración de respaldo.");
            }

            var fallback = ReadFallbackConfiguration();
            if (fallback is not null && !candidates.Any(candidate => IsSameServer(candidate, fallback)))
                candidates.Add(fallback);

            return candidates;
        }

        private SmtpSettingsModel? ReadFallbackConfiguration()
        {
            string host = _configuration["SmtpSettings:Server"] ?? string.Empty;
            string email = _configuration["SmtpSettings:SenderEmail"] ?? string.Empty;
            string password = _configuration["SmtpSettings:Password"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return null;

            return new SmtpSettingsModel
            {
                Name = "Configuración de respaldo",
                Host = host,
                Port = int.TryParse(_configuration["SmtpSettings:Port"], out var port) ? port : 465,
                Email = email,
                Password = password,
                UseSsl = bool.TryParse(_configuration["SmtpSettings:UseSsl"], out var useSsl) && useSsl,
                EnableBcc = bool.TryParse(_configuration["SmtpSettings:EnableBcc"], out var enableBcc) && enableBcc,
                BccEmail = _configuration["SmtpSettings:BccEmail"] ?? string.Empty
            };
        }

        private static bool IsSameServer(SmtpSettingsModel first, SmtpSettingsModel second)
        {
            return first.Port == second.Port
                && string.Equals(first.Host.Trim(), second.Host.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(first.Email.Trim(), second.Email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
