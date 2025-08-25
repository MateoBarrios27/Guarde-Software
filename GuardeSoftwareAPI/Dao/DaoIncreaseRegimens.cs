using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
	public class DaoIncreaseRegimens
	{
        private readonly AccessDB accessDB;

        public DaoIncreaseRegimens(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetIncreaseRegimens()
        {
            string query = "SELECT regimen_id, frequency, percentage FROM increase_regimens";

            return accessDB.GetTable("increase_regimens",query);
        }

        public DataTable GetIncreaseRegimensById(int id) { 
        
            string query = "SELECT regimen_id, frequency, percentage FROM increase_regimens WHERE regimen_id = @regimen_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@regimen_id", SqlDbType.Int){Value = id},
            };

            return accessDB.GetTable("increase_regimens", query,parameters);
        }

    }
}
