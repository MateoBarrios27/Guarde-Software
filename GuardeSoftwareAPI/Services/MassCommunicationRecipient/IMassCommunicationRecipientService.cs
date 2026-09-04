using GuardeSoftwareAPI.Dtos.MassCommunicationRecipient;

namespace GuardeSoftwareAPI.Services.massCommunicationRecipient
{
    public interface IMassCommunicationRecipientService
    {
        Task<List<MassCommunicationRecipientDto>> GetAllAsync();
        Task<MassCommunicationRecipientDto?> GetByIdAsync(int id);
        Task<MassCommunicationRecipientDto> CreateAsync(UpsertMassCommunicationRecipientDto dto);
        Task<MassCommunicationRecipientDto?> UpdateAsync(int id, UpsertMassCommunicationRecipientDto dto);
        Task<bool> DeleteAsync(int id);
        Task<MassCommunicationRecipientImportResultDto> ImportAsync(MassCommunicationRecipientImportRequest request);
    }
}
