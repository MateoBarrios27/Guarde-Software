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
		public User GetUserById(int userId)
		{
			if (userId <= 0) throw new ArgumentException("Invalid user ID.");

			DataTable userTable = _daoUser.GetUserById(userId);

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
		
		public bool CreateUser(User user)
		{
			if (user == null) throw new ArgumentNullException(nameof(user), "User cannot be null.");
			if (user.UserTypeId <= 0) throw new ArgumentException("Invalid user type ID."); //Missing validation for user type existence
			if (string.IsNullOrWhiteSpace(user.UserName)) throw new ArgumentException("Username cannot be empty.");
			// if (string.IsNullOrWhiteSpace(user.FirstName)) throw new ArgumentException("First name cannot be empty."); Now, first name can be null
			// if (string.IsNullOrWhiteSpace(user.LastName)) throw new ArgumentException("Last name cannot be empty."); Now, last name can be null
			if (string.IsNullOrWhiteSpace(user.PasswordHash)) throw new ArgumentException("Password hash cannot be empty.");
			user.PasswordHash = Utilities.HashUtility.ComputeSha256Hash(user.PasswordHash);
			// Check if username already exists
			DataTable existingUserTable = _daoUser.GetUserByUsername(user.UserName);
			if (existingUserTable.Rows.Count > 0) throw new ArgumentException("Username already exists.");
			if (_daoUser.CreateUser(user)) return true;
			else return false;
		}

		public bool DeleteUser(int userId)
		{
			if (userId <= 0) throw new ArgumentException("Invalid user ID.");
			if (_daoUser.DeleteUser(userId)) return true;
			else return false;
		}
	}
}