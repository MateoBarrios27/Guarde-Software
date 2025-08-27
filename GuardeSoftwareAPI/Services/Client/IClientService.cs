using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.client
{

	public interface IClientService
	{
        List<Client> GetClientsList();

		List<Client> GetClientListById(int id);
 	}
}