using System;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.userType
{

	public class UserTypeService : IUserTypeService
	{
		readonly DaoUserType _daoUserType;
		public UserTypeService(AccessDB accessDB)
		{
			_daoUserType = new DaoUserType(accessDB);
		}

		public List<UserType> GetUserTypeList()
		{
			DataTable userTypeTable = _daoUserType.GetUserTypes();
			List<UserType> userTypes = new List<UserType>();

			if (userTypeTable.Rows.Count == 0) throw new ArgumentException("No user types found.");

			foreach (DataRow row in userTypeTable.Rows)
			{
				int userTypeId = (int)row["user_type_id"];

				UserType userType = new UserType
				{
					Id = userTypeId,
					Name = row["name"]?.ToString() ?? string.Empty,
				};

				userTypes.Add(userType);
			}

			return userTypes;
		}
		
		public UserType GetUserTypeById(int id)
		{
			if (id <= 0) throw new ArgumentException("Invalid user type ID.");

			DataTable userTypeTable = _daoUserType.GetUserTypeById(id);

			if (userTypeTable.Rows.Count == 0) throw new ArgumentException("No user type found with the given ID.");

			DataRow row = userTypeTable.Rows[0];

			return new UserType
			{
				Id = row["user_type_id"] != DBNull.Value ? (int)row["user_type_id"] : 0,
				Name = row["name"]?.ToString() ?? string.Empty
			};
		}
	}
}
