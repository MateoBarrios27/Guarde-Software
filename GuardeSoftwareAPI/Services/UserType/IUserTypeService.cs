using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.userType
{

	public interface IUserTypeService
	{
		public List<UserType> GetUserTypeList();
		public UserType GetUserTypeById(int id);
		public Task<bool> CreateUserType(UserType userType);
		public bool DeleteUserType(int userTypeId);
		
	}
}