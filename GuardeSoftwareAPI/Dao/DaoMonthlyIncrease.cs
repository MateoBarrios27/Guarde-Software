using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoMonthlyIncrease
    {
        private readonly AccessDB _accessDB;

        public DaoMonthlyIncrease(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        public async Task<DataTable> GetAllAsync()
        {
            string query = "SELECT * FROM monthly_increase_settings ORDER BY effective_date DESC";
            return await _accessDB.GetTableAsync("monthly_increase_settings", query);
        }

        public async Task<DataRow?> GetByIdAsync(int id)
        {
            string query = "SELECT * FROM monthly_increase_settings WHERE increase_setting_id = @Id";
            SqlParameter[] parameters = { new SqlParameter("@Id", id) };
            DataTable table = await _accessDB.GetTableAsync("monthly_increase_setting", query, parameters);
            return table.Rows.Count > 0 ? table.Rows[0] : null;
        }

        /// <summary>
        /// Obtains the increase percentage for a specific month, if defined.
        /// </summary>
        public async Task<decimal?> GetIncreasePercentageForMonthAsync(DateTime monthDate, SqlConnection connection, SqlTransaction? transaction = null)
        {
            string query = "SELECT percentage FROM monthly_increase_settings WHERE effective_date = @EffectiveDate";
            var firstDayOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
            
            SqlParameter[] parameters = {
                new SqlParameter("@EffectiveDate", SqlDbType.Date) { Value = firstDayOfMonth }
            };

            object result;
            if (transaction != null)
            {
                 using (var command = new SqlCommand(query, connection, transaction))
                 {
                    command.Parameters.AddRange(parameters);
                    result = await command.ExecuteScalarAsync();
                 }
            }
            else
            {
                // Este método (ExecuteScalarAsync) podria ser modificado en AccessDB para aceptar solo connection
                // Por ahora, asumimos que AccessDB puede manejar una conexión abierta
                result = await _accessDB.ExecuteScalarAsync(query, parameters); // Simplificación
            }
            
            return (result != null && result != DBNull.Value) ? (decimal?)Convert.ToDecimal(result) : null;
        }

        /// <summary>
        /// Obtains all applicable increases between two dates.
        /// </summary>
        public async Task<Dictionary<DateTime, decimal>> GetApplicableIncreasesBetweenDatesAsync(DateTime startDate, DateTime endDate, SqlConnection connection, SqlTransaction transaction)
        {
            var increases = new Dictionary<DateTime, decimal>();
            string query = "SELECT effective_date, percentage FROM monthly_increase_settings WHERE effective_date > @StartDate AND effective_date <= @EndDate";
            SqlParameter[] parameters = {
                new SqlParameter("@StartDate", SqlDbType.Date) { Value = startDate },
                new SqlParameter("@EndDate", SqlDbType.Date) { Value = endDate }
            };

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        increases[reader.GetDateTime(0)] = reader.GetDecimal(1);
                    }
                }
            }
            return increases;
        }

        public async Task<bool> IsMonthAlreadyConfiguredAsync(DateTime effectiveDate, int? excludeId = null)
        {
            var firstDayOfMonth = new DateTime(effectiveDate.Year, effectiveDate.Month, 1);
            string query = "SELECT COUNT(1) FROM monthly_increase_settings WHERE effective_date = @EffectiveDate";
            
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@EffectiveDate", SqlDbType.Date) { Value = firstDayOfMonth }
            };

            if (excludeId.HasValue)
            {
                query += " AND increase_setting_id != @ExcludeId";
                parameters.Add(new SqlParameter("@ExcludeId", SqlDbType.Int) { Value = excludeId.Value });
            }

            object result = await _accessDB.ExecuteScalarAsync(query, parameters.ToArray());
            return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) > 0 : false;
        }

        public async Task<int> CreateAsync(DateTime effectiveDate, decimal percentage)
        {
            string query = @"
                INSERT INTO monthly_increase_settings (effective_date, percentage) 
                OUTPUT INSERTED.increase_setting_id 
                VALUES (@EffectiveDate, @Percentage)";
            
            var firstDayOfMonth = new DateTime(effectiveDate.Year, effectiveDate.Month, 1);
            
            SqlParameter[] parameters = {
                new SqlParameter("@EffectiveDate", SqlDbType.Date) { Value = firstDayOfMonth },
                new SqlParameter("@Percentage", SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = percentage },
            };

            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToInt32(result);
        }

        public async Task<bool> UpdateAsync(int id, decimal percentage)
        {
            string query = "UPDATE monthly_increase_settings SET percentage = @Percentage, created_at = GETDATE() WHERE increase_setting_id = @Id";
            SqlParameter[] parameters = {
                new SqlParameter("@Percentage", SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = percentage },
                new SqlParameter("@Id", SqlDbType.Int) { Value = id }
            };
            return await _accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            string query = "DELETE FROM monthly_increase_settings WHERE increase_setting_id = @Id";
            SqlParameter[] parameters = { new SqlParameter("@Id", SqlDbType.Int) { Value = id } };
            return await _accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
    }
}