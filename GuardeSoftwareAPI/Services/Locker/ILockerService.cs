using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.locker
{

	public interface ILockerService
	{
		List<Locker> GetLockersList();

		List<Locker> GetLockerListById(int id);
	}
}