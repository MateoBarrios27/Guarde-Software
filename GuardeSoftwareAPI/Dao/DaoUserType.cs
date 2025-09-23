using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoUserType
    {
        private readonly AccessDB accessDB;

        public DaoUserType(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetUserTypes()
        {
            string query = "SELECT user_type_id, name FROM user_types";

            return await accessDB.GetTableAsync("user_types", query);
        }

        public async Task<DataTable> GetUserTypeById(int userTypeId)
        {

            string query = "SELECT user_type_id, name FROM user_types WHERE user_type_id = @user_type_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("user_type_id", SqlDbType.Int){Value = userTypeId},
            };

            return await accessDB.GetTableAsync("user_types", query, parameters);
        }
        
        public async Task<bool> CreateUserTypeAsync(UserType userType) {
            string query = "INSERT INTO user_types (name) VALUES (@name)";
            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@name", SqlDbType.NVarChar, 100){Value  = userType.Name},
            };
            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<bool> CheckIfUserTypeNameExistsAsync(string name)
        {
            string query = "SELECT COUNT(*) FROM user_types WHERE name = @name";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@name", SqlDbType.NVarChar, 100) { Value = name }
            };

            object result = await accessDB.ExecuteScalarAsync(query, parameters);
            int count = (result != null && int.TryParse(result.ToString(), out int tempCount)) ? tempCount : 0;

            return count > 0;
        }
        
        
         public async Task<bool> DeleteUserType(int userTypeId)
        {

            string query = "UPDATE user_types SET active = 0 WHERE user_type_id = @user_type_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("user_type_id", SqlDbType.Int){Value = userTypeId},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
    }
}
