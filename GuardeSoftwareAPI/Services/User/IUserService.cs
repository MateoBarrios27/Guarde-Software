using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.user
{

	public interface IUserService
	{
		public List<User> GetUserList();
		public User GetUserById(int id);
		public bool DeleteUser(int userId);
	}
}