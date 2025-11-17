using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Services.communication
{
    /** Servicio para la lógica de negocio (acceso a DB) */
    public interface ICommunicationService
    {
        Task<List<CommunicationDto>> GetCommunications();
        Task<CommunicationDto> GetCommunicationById(int id);
        
        // --- Firmas actualizadas para FormData ---
        Task<CommunicationDto> CreateCommunicationAsync(UpsertCommunicationRequest request, List<AttachmentDto> uploadedFiles, int userId);
        Task<CommunicationDto> UpdateCommunicationAsync(int id, UpsertCommunicationRequest request, List<AttachmentDto> newFiles, int userId);
        
        Task<bool> DeleteCommunicationAsync(int id);
        
        // --- Nuevos métodos requeridos por el frontend ---
        Task<CommunicationDto> SendDraftNowAsync(int id);
        Task<CommunicationDto> RetryFailedSendsAsync(int communicationId, string mailServerId);
        Task DeleteAttachmentAsync(int communicationId, string fileName);
        
        Task<List<ClientCommunicationDto>> GetCommunicationsByClientIdAsync(int clientId);
    }
}