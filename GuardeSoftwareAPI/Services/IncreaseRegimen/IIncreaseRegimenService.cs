using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.increaseRegimen
{

	public interface IIncreaseRegimenService
	{
		Task<List<IncreaseRegimen>> GetIncreaseRegimensList();

		Task<List<IncreaseRegimen>> GetIncreaseRegimenListById(int id);

		Task<bool> CreateIncreaseRegimen(IncreaseRegimen increaseRegimen);
	}
}