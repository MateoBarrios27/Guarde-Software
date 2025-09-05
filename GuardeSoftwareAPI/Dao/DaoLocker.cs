using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;

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

        public bool CreateLocker(Locker locker)
        {

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@warehouse_id",SqlDbType.Int){Value = locker.WarehouseId},
                new SqlParameter("@locker_type_id",SqlDbType.Int){Value = locker.LockerTypeId},
                new SqlParameter("@identifier",SqlDbType.VarChar,100){Value = (object?)locker.Identifier ?? DBNull.Value},
                new SqlParameter("@features",SqlDbType.VarChar){Value = (object?)locker.Features ?? DBNull.Value},
                new SqlParameter("@status",SqlDbType.VarChar,50){Value = locker.Status},
            };

            string query = "INSERT INTO lockers(warehouse_id,locker_type_id, identifier, features, status)VALUES(@warehouse_id,@locker_type_id, @identifier, @features, @status)";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

    }
}