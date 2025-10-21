using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Services.communication
{
    public interface ICommunicationService
    {
        Task<List<CommunicationDto>> GetCommunications();
        Task<CommunicationDto> GetCommunicationById(int id);
        Task<CommunicationDto> CreateCommunicationAsync(UpsertCommunicationRequest request, int userId);
    }
}