using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Client;

namespace GuardeSoftwareAPI.Services.client
{

    public interface IClientService
    {
        List<Client> GetClientsList();

        List<Client> GetClientListById(int id);

        Task<int> CreateClientAsync(CreateClientDTO dto);
        public Task<GetClientDetailDTO> GetClientDetailByIdAsync(int id);
    }
}