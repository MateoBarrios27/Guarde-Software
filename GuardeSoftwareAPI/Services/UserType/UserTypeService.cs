using System;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Services.userType
{

	public class UserTypeService : IUserTypeService
    {
		readonly DaoUserType _daoUserType;
		public UserTypeService(AccessDB accessDB)
		{
			_daoUserType = new DaoUserType(accessDB);
		}
	}
}
