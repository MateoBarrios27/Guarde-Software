using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Dao
{

    public class DaoLockerType
    {
        private readonly AccessDB accessDB;

        public DaoLockerType(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetLockerTypes()
        {
            string query = "SELECT locker_type_id, name, amount, m3  FROM locker_types WHERE active = 1";

            return accessDB.GetTable("locker_types", query);
        }

        public DataTable GetLockerTypeById(int id)
        {

            string query = "SELECT locker_type_id, name, amount, m3  FROM locker_types WHERE active = 1 AND locker_type_id = @locker_type_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@locker_type_id", SqlDbType.Int){Value  = id}
            };

            return accessDB.GetTable("locker_types", query, parameters);
        }

        public bool DeleteLockerType(int id)
        {

            string query = "UPDATE locker_types SET active = 0 WHERE locker_type_id = @locker_type_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@locker_type_id", SqlDbType.Int){Value  = id},
            };

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

        public async Task<bool> CreateLockerType(LockerType lockerType)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
               new SqlParameter("@name", SqlDbType.VarChar) {Value = lockerType.Name},
               new SqlParameter("@amount", SqlDbType.Decimal) {Value = lockerType.Amount},
               new SqlParameter("@m3", SqlDbType.Decimal) {Value = (object?)lockerType.M3 ?? DBNull.Value},
            };

            string query = "INSERT INTO locker_types(name, amount, m3)VALUES(@name, @amount, @m3)";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
        
        public async Task<bool> CheckIfLockerTypeNameExistsAsync(string name)
        {
            string query = "SELECT COUNT(*) FROM locker_types WHERE name = @name AND active = 1";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@name", SqlDbType.VarChar) { Value = name }
            };

            object result = await accessDB.ExecuteScalarAsync(query, parameters);
            int count = Convert.ToInt32(result);

            return count > 0;
        }
    }
}
