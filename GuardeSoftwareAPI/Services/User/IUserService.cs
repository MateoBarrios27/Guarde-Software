using System;
using GuardeSoftwareAPI.Dtos.User;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.user
{

	public interface IUserService
	{
		Task<List<User>> GetUserList();
		Task<User> GetUserById(int id);
		Task<User> CreateUser(User user, string password);
		Task<bool> DeleteUser(int userId);
		Task<bool> UpdateUser(User user);
	}
}