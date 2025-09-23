using System;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Services.clientIncreaseRegimen
{

	public interface IClientIncreaseRegimenService
	{
		Task<List<ClientIncreaseRegimen>> GetClientIncreaseRegimensList();

        Task<List<ClientIncreaseRegimen>> GetClientIncreaseRegimensListByClientId(int id);

		Task<bool> CreateClientIncreaseRegimen(ClientIncreaseRegimen clientIncreaseRegimen);
    }
}