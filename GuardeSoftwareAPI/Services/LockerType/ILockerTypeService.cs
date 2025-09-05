using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.lockerType
{

	public interface ILockerTypeService
	{
		List<LockerType> GetLockerTypesList();

        List<LockerType> GetLockerTypeListById(int id);

		public bool CreateLockerType(LockerType lockerType);
    }
}