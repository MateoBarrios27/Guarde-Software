using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao { 

    public class DaoLocker
	{
        private readonly AccessDB accessDB;

        public DaoLocker(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetLockers()
        {
            string query = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status FROM lockers";

            return accessDB.GetTable("lockers", query);
        }

        public DataTable GetLockerById(int id) {

            string query = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status FROM lockers WHERE locker_id = @locker_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@locker_id", SqlDbType.Int){Value = id},
            };

            return accessDB.GetTable("lockers", query, parameters);
        }
    }
}