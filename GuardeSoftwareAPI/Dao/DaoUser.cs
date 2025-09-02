using System;
using System.Data;
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

        public DataTable GetUsers()
        {
            string query = "SELECT user_id, user_type_id, username,first_name,last_name FROM users WHERE active = 1";

            return accessDB.GetTable("users", query);
        }

        public DataTable GetUserById(int userid) {

            string query = "SELECT user_id, user_type_id, username,first_name,last_name FROM users WHERE active = 1 AND user_id = @user_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("user_id", SqlDbType.Int){Value = userid},
            };

            return accessDB.GetTable("users", query, parameters);
        }

        public DataTable GetUserByUsername(string username)
        {
            string query = "SELECT user_id, user_type_id, username,first_name,last_name FROM users WHERE active = 1 AND username = @username";

            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("username", SqlDbType.NVarChar, 100) { Value = username },
            };
            return accessDB.GetTable("users", query, parameters);
        }

        //This method creates a new user in the database with password
        //The password should be hashed before calling this method
        public bool CreateUser(User user)
        {

            string query = "INSERT INTO users (user_type_id, username, first_name, last_name, password) VALUES (@user_type_id, @username, @first_name, @last_name, @password)";
            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("user_type_id", SqlDbType.Int){Value = user.UserTypeId},
                new SqlParameter("username", SqlDbType.NVarChar, 100){Value = user.UserName},
                new SqlParameter("first_name", SqlDbType.NVarChar, 100){Value = user.FirstName},
                new SqlParameter("last_name", SqlDbType.NVarChar, 100){Value = user.LastName},
                new SqlParameter("password", SqlDbType.NVarChar, 255){Value = user.PasswordHash},
            };
            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

        public bool DeleteUser(int userId)
        {

            string query = "UPDATE users SET active = 0 WHERE user_id = @user_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("user_id", SqlDbType.Int){Value = userId},
            };

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }
    }
}
