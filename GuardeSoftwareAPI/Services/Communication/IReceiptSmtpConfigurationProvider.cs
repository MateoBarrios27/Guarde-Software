using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Services.communication
{
    public interface IReceiptSmtpConfigurationProvider
    {
        Task<IReadOnlyList<SmtpSettingsModel>> GetCandidatesAsync(
            CancellationToken cancellationToken = default);
    }
}
