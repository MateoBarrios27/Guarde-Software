using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoIncreaseRegimen
	{
        private readonly AccessDB accessDB;

        public DaoIncreaseRegimen(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetIncreaseRegimens()
        {
            string query = "SELECT regimen_id, frequency, percentage FROM increase_regimens";

            return accessDB.GetTable("increase_regimens",query);
        }

        public DataTable GetIncreaseRegimenById(int id) { 
        
            string query = "SELECT regimen_id, frequency, percentage FROM increase_regimens WHERE regimen_id = @regimen_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@regimen_id", SqlDbType.Int){Value = id},
            };

            return accessDB.GetTable("increase_regimens", query,parameters);
        }

        public bool CreateIncreaseRegimen(IncreaseRegimen increaseRegimen)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                 new SqlParameter("@frequency", SqlDbType.Int){Value = increaseRegimen.Frequency },
                 new SqlParameter("@Percentage", SqlDbType.Decimal){Value = increaseRegimen.Percentage },
            };

            string query = "INSERT INTO increase_regimens(frequency, percentage)VALUES(@frequency, @percentage)";

            return accessDB.ExecuteCommand(query, parameters) > 0;

        }
    }
}
