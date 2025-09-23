using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.user
{

	public interface IUserService
	{
		Task<List<User>> GetUserList();
		Task<User> GetUserById(int id);
		Task<bool> CreateUser(User user);
		Task<bool> DeleteUser(int userId);
	}
}