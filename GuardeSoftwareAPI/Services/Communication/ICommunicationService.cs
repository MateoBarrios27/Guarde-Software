using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Services.communication
{
    public interface ICommunicationService
    {
        Task<List<CommunicationDto>> GetCommunications();
        Task<CommunicationDto> GetCommunicationById(int id);
        Task<CommunicationDto> CreateCommunicationAsync(UpsertCommunicationRequest request, int userId);
        Task<CommunicationDto> SendDraftNowAsync(int communicationId);
        Task<bool> DeleteCommunicationAsync(int communicationId);
        Task<CommunicationDto> UpdateCommunicationAsync(int communicationId, UpsertCommunicationRequest request, int userId);
        Task<List<ClientCommunicationDto>> GetCommunicationsByClientIdAsync(int clientId);
    }
}