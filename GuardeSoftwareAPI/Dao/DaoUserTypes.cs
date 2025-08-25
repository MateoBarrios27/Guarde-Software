using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
	public class DaoUserTypes
	{
        private readonly AccessDB accessDB;

        public DaoUserTypes(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetUserTypes()
        {
            string query = "SELECT user_type_id, name FROM user_types";

            return accessDB.GetTable("user_types", query);
        }

        public DataTable GetUserTypeById(int userTypeId) {

            string query = "SELECT user_type_id, name FROM user_types WHERE user_type_id = @user_type_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("user_type_id", SqlDbType.Int){Value = userTypeId},
            };

            return accessDB.GetTable("user_types",query, parameters);
        }
    }
}
