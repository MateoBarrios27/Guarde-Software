using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{

	public class DaoLockerTypes
	{
        private readonly AccessDB accessDB;

        public DaoLockerTypes(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetLockerTypes()
        {
            string query = "SELECT locker_type_id, name, amount, m3  FROM locker_types WHERE active = 1";

            return accessDB.GetTable("locker_types", query);
        }

        public DataTable GetLockerTypeById(int id) {

            string query = "SELECT locker_type_id, name, amount, m3  FROM locker_types WHERE active = 1 AND locker_type_id = @locker_type_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@locker_type_id", SqlDbType.Int){Value  = id}
            };

            return accessDB.GetTable("locker_types", query, parameters);
        }
    }
}
