using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.lockerType
{

	public interface ILockerTypeService
	{
		Task<List<LockerType>> GetLockerTypesList();

        Task<List<LockerType>> GetLockerTypeListById(int id);

		Task<bool> CreateLockerType(LockerType lockerType);
    }
}