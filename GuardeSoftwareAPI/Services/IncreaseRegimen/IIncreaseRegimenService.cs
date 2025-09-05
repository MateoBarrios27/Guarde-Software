using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.increaseRegimen
{

	public interface IIncreaseRegimenService
	{
		List<IncreaseRegimen> GetIncreaseRegimensList();

		List<IncreaseRegimen> GetIncreaseRegimenListById(int id);

		public bool CreateIncreaseRegimen(IncreaseRegimen increaseRegimen);
	}
}