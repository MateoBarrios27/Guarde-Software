using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Dtos.Common;

namespace GuardeSoftwareAPI.Services.client
{

    public interface IClientService
    {
        Task<List<Client>> GetClientsList();
        Task<List<Client>> GetClientListById(int id);
        Task<int> CreateClientAsync(CreateClientDTO dto);
        Task<GetClientDetailDTO> GetClientDetailByIdAsync(int id);
        Task<PaginatedResultDto<GetTableClientsDto>> GetClientsTableAsync(GetClientsRequestDto request);
        Task<List<string>> GetClientRecipientNamesAsync();
        Task<List<string>> SearchClientNamesAsync(string query);
        Task<bool> UpdateClientAsync(int id, CreateClientDTO dto);
        Task<bool> DeactivateClientAsync(int clientId);
    }
}