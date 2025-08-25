using System;
using System.Data;


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
    }
}
