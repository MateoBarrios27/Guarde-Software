using System;
using System.Data;
using System.Threading.Tasks;
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

		public async Task<List<UserType>> GetUserTypeList()
		{
			DataTable userTypeTable = await _daoUserType.GetUserTypes();
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

		public async Task<UserType> GetUserTypeById(int userTypeId)
		{
			if (userTypeId <= 0) throw new ArgumentException("Invalid user type ID.");

			DataTable userTypeTable = await _daoUserType.GetUserTypeById(userTypeId);

			if (userTypeTable.Rows.Count == 0) throw new ArgumentException("No user type found with the given ID.");

			DataRow row = userTypeTable.Rows[0];

			return new UserType
			{
				Id = row["user_type_id"] != DBNull.Value ? (int)row["user_type_id"] : 0,
				Name = row["name"]?.ToString() ?? string.Empty
			};
		}

		public async Task<UserType> CreateUserType(UserType userType)
		{
			if (userType == null) throw new ArgumentNullException(nameof(userType), "User type cannot be null.");
			if (string.IsNullOrWhiteSpace(userType.Name)) throw new ArgumentException("User type name cannot be empty.");
			if (await _daoUserType.CheckIfUserTypeNameExistsAsync(userType.Name)) throw new ArgumentException("A user type with the same name already exists.");
			return await _daoUserType.CreateUserTypeAsync(userType);
		}

		public async Task<bool> DeleteUserType(int userTypeId)
		{
			if (userTypeId <= 0) throw new ArgumentException("Invalid user type ID.");
			if (await _daoUserType.DeleteUserType(userTypeId)) return true;
			else return false;
		}
	}
}
