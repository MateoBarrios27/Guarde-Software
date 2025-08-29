using System;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.user
{

	public class UserService : IUserService
	{
		readonly DaoUser _daoUser;
		public UserService(AccessDB accessDB)
		{
			_daoUser = new DaoUser(accessDB);
		}

		// Get a list of all users but without the password hash
		public List<User> GetUserList()
		{
			DataTable userTable = _daoUser.GetUsers();
			List<User> users = new List<User>();

			if (userTable.Rows.Count == 0) throw new ArgumentException("No users found.");

			foreach (DataRow row in userTable.Rows)
			{
				int userId = (int)row["user_id"];

				User user = new User
				{
					Id = userId,
					UserTypeId = row["user_type_id"] != DBNull.Value ? (int)row["user_type_id"] : 0,
					UserName = row["username"]?.ToString() ?? string.Empty,
					FirstName = row["first_name"]?.ToString() ?? string.Empty,
					LastName = row["last_name"]?.ToString() ?? string.Empty,
					PasswordHash = string.Empty // Do not expose password hash
				};

				users.Add(user);
			}

			return users;
		}
		
		// Get a user by ID but without the password hash
		public User GetUserById(int id)
		{
			if (id <= 0) throw new ArgumentException("Invalid user ID.");

			DataTable userTable = _daoUser.GetUserById(id);

			if (userTable.Rows.Count == 0) throw new ArgumentException("No user found with the given ID.");

			DataRow row = userTable.Rows[0];

			return new User
			{
				Id = row["user_id"] != DBNull.Value ? (int)row["user_id"] : 0,
				UserTypeId = row["user_type_id"] != DBNull.Value ? (int)row["user_type_id"] : 0,
				UserName = row["username"]?.ToString() ?? string.Empty,
				FirstName = row["first_name"]?.ToString() ?? string.Empty,
				LastName = row["last_name"]?.ToString() ?? string.Empty,
				PasswordHash = string.Empty // Do not expose password hash
			};
		}
	}
}