using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.userType
{

	public interface IUserTypeService
	{
		public List<UserType> GetUserTypeList();
		public UserType GetUserTypeById(int id);
		
	}
}