using System;
using System.Data;


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
            string consult = "SELECT user_type_id, name FROM user_types";

            return accessDB.GetTable("user_types",consult);
        }
    }
}
