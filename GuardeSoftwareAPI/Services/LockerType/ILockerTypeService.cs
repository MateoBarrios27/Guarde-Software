using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.lockerType
{

	public interface ILockerTypeService
	{
		Task<List<LockerType>> GetLockerTypesList();
        Task<List<LockerType>> GetLockerTypeListById(int id);
		Task<LockerType> CreateLockerType(LockerType lockerType);
		Task<bool> UpdateLockerType(LockerType lockerType);
		Task<bool> DeleteLockerType(int id);
    }
}