using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.User;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao 
{ 
    public class DaoUser
	{
        private readonly AccessDB accessDB;

        public DaoUser(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetUsers()
        {
            string query = "SELECT user_id, user_type_id, username,first_name,last_name FROM users WHERE active = 1";

            return await accessDB.GetTableAsync("users", query);
        }

        public async Task<DataTable> GetUserById(int userid) {

            string query = "SELECT user_id, user_type_id, username,first_name,last_name FROM users WHERE active = 1 AND user_id = @user_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("user_id", SqlDbType.Int){Value = userid},
            };

            return await accessDB.GetTableAsync("users", query, parameters);
        }

        public async Task<DataTable> GetUserByUsername(string username)
        {
            string query = "SELECT user_id, user_type_id, username,first_name,last_name FROM users WHERE active = 1 AND username = @username";

            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("username", SqlDbType.NVarChar, 100) { Value = username },
            };
            return await accessDB.GetTableAsync("users", query, parameters);
        }

        //This method creates a new user in the database with password
        //The password should be hashed before calling this method
        public async Task<User> CreateUser(User user)
        {
            string query = "INSERT INTO users (user_type_id, username, first_name, last_name, password) VALUES (@user_type_id, @username, @first_name, @last_name, @password); SELECT SCOPE_IDENTITY();";

            SqlParameter[] parameters = [
                new SqlParameter("user_type_id", SqlDbType.Int){Value = user.UserTypeId},
                new SqlParameter("username", SqlDbType.NVarChar, 100){Value = user.UserName},
                new SqlParameter("first_name", SqlDbType.NVarChar, 100){Value = user.FirstName},
                new SqlParameter("last_name", SqlDbType.NVarChar, 100){Value = user.LastName},
                new SqlParameter("password", SqlDbType.NVarChar, 255){Value = user.PasswordHash},
            ];
            object newId = await accessDB.ExecuteScalarAsync(query, parameters);

            if (newId != null && newId != DBNull.Value)
            {
                //Assign the newly generated ID to the user object
                user.Id = Convert.ToInt32(newId);
            }

            return user;
        }

        public async Task<bool> DeleteUser(int userId)
        {

            string query = "UPDATE users SET active = 0 WHERE user_id = @user_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("user_id", SqlDbType.Int){Value = userId},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
    }
}
