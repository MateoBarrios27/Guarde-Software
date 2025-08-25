using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao 
{ 
    public class DaoUsers
	{
        private readonly AccessDB accessDB;

        public DaoUsers(AccessDB _accessDB)
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

        public void DeleteUser(int userid) {

            string query = "UPDATE users SET active = 0 WHERE user_id = @user_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("user_id", SqlDbType.Int){Value = userid},
            };

            accessDB.ExecuteCommand(query, parameters);
        }
    }
}
