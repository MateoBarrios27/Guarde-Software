using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;


namespace GuardeSoftwareAPI.Dao
{

    public class DaoLockerType
    {
        private readonly AccessDB accessDB;

        public DaoLockerType(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetLockerTypes()
        {
            string query = "SELECT locker_type_id, name, m3  FROM locker_types WHERE active = 1";

            return await accessDB.GetTableAsync("locker_types", query);
        }

        public async Task<DataTable> GetLockerTypeById(int id)
        {

            string query = "SELECT locker_type_id, name, m3  FROM locker_types WHERE active = 1 AND locker_type_id = @locker_type_id";

            SqlParameter[] parameters = [

                new SqlParameter("@locker_type_id", SqlDbType.Int){Value  = id}
            ];

            return await accessDB.GetTableAsync("locker_types", query, parameters);
        }

        public async Task<LockerType> CreateLockerType(LockerType lockerType)
        {
            SqlParameter[] parameters =
            [
               new SqlParameter("@name", SqlDbType.VarChar) {Value = lockerType.Name},
               new SqlParameter("@m3", SqlDbType.Decimal) {Value = (object?)lockerType.M3 ?? DBNull.Value},
            ];

            string query = "INSERT INTO locker_types(name, m3)VALUES(@name, @m3); SELECT SCOPE_IDENTITY()";

            object newId = await accessDB.ExecuteScalarAsync(query, parameters);

            if (newId != null && newId != DBNull.Value)
            {
                lockerType.Id = Convert.ToInt32(newId);
            }

            return lockerType;
        }

        public async Task<bool> UpdateLockerType(LockerType lockerType)
        {
            string query = "UPDATE locker_types SET name = @name, m3 = @m3 WHERE locker_type_id = @locker_type_id";

            SqlParameter[] parameters =
            [
                new SqlParameter("@name", SqlDbType.VarChar) { Value = lockerType.Name },
                new SqlParameter("@m3", SqlDbType.Decimal) { Value = (object?)lockerType.M3 ?? DBNull.Value },
                new SqlParameter("@locker_type_id", SqlDbType.Int) { Value = lockerType.Id }
            ];

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<bool> DeleteLockerType(int id)
        {

            string query = "UPDATE locker_types SET active = 0 WHERE locker_type_id = @locker_type_id";

            SqlParameter[] parameters = [

                new SqlParameter("@locker_type_id", SqlDbType.Int){Value  = id},
            ];

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        
        
        public async Task<bool> CheckIfLockerTypeNameExistsAsync(string name)
        {
            string query = "SELECT COUNT(*) FROM locker_types WHERE name = @name AND active = 1";

            SqlParameter[] parameters =
            [
                new SqlParameter("@name", SqlDbType.VarChar) { Value = name }
            ];

            object result = await accessDB.ExecuteScalarAsync(query, parameters);
            int count = Convert.ToInt32(result);

            return count > 0;
        }

        public async Task<bool> CheckIfLockerTypeNameExistsAsync(string name, int id)
        {
            string query = "SELECT COUNT(*) FROM locker_types WHERE name = @name AND locker_type_id != @locker_type_id AND active = 1";

            SqlParameter[] parameters =
            [
                new SqlParameter("@name", SqlDbType.VarChar) { Value = name },
                new SqlParameter("@locker_type_id", SqlDbType.Int) { Value = id }
            ];

            object result = await accessDB.ExecuteScalarAsync(query, parameters);
            int count = Convert.ToInt32(result);

            return count > 0;
        }
    }
}
