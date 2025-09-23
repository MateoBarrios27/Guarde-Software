using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Client;

namespace GuardeSoftwareAPI.Services.client
{

    public interface IClientService
    {
        Task<List<Client>> GetClientsList();

        Task<List<Client>> GetClientListById(int id);

        Task<int> CreateClientAsync(CreateClientDTO dto);
        Task<GetClientDetailDTO> GetClientDetailByIdAsync(int id);
    }
}