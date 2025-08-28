using System;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Services.user
{

	public class UserService : IUserService
    {
		readonly DaoUser _daoUser;
		public UserService(AccessDB accessDB)
		{
			_daoUser = new DaoUser(accessDB);
		}
	}
}