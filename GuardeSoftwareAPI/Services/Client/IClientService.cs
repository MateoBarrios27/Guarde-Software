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
        Task ReactivateClientAsync(int clientId, CreateClientDTO dto);
        Task<List<ClientLockerHistory>> GetClientLockerHistoryAsync(int clientId);
        Task<bool> UpdateClientColorAsync(int clientId, string? color);
        Task<bool> UpdateClientCommentAsync(int clientId, string? comment);
        Task<bool> UpdateClientNotesAsync(int clientId, string? notes);
        Task<List<RentalAmountHistoryItemDto>> GetClientRentalAmountHistoryAsync(int clientId);
        Task AddClientRentalAmountEntryAsync(int clientId, decimal amount, int year, int month);
        Task UpdateClientRentalAmountEntryAsync(int clientId, int histId, decimal amount, int year, int month);
        Task DeleteClientRentalAmountEntryAsync(int clientId, int histId);
    }
}