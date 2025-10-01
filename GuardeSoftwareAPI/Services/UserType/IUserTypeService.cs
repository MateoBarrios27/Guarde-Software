using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.userType
{

	public interface IUserTypeService
	{
		Task<List<UserType>> GetUserTypeList();
		Task<UserType> GetUserTypeById(int id);
		Task<UserType> CreateUserType(UserType userType);
		Task<bool> DeleteUserType(int userTypeId);
		
	}
}