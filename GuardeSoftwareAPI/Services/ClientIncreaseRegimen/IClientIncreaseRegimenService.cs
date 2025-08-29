using System;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Services.clientIncreaseRegimen
{

	public interface IClientIncreaseRegimenService
	{
		List<ClientIncreaseRegimen> GetClientIncreaseRegimensList();

        List<ClientIncreaseRegimen> GetClientIncreaseRegimensListByClientId(int id);
    }
}