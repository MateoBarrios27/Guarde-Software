using System;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoUserTypes
	{
        private readonly AccessDB accessDB;

        public DaoUserTypes(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public GetUserTypes()
        {
            string consult = "SELECT user_type_id, name FROM user_types";

            return accessDB.GetTable("user_types",consult);
        }
    }
}
