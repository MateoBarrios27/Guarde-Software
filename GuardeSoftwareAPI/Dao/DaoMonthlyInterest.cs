using System.Data;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Utils;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    /// <summary>
    /// Monthly interest configuration (monthly_interest_settings) and the record of months
    /// already applied by the interests job (monthly_interest_runs).
    /// The reads used by the job and by payment registration tolerate the tables not existing
    /// yet (migration Update_Add_MonthlyInterestSettings.sql not applied): they fall back to the
    /// historical 10 % / day 11 instead of breaking payments or the job.
    /// </summary>
    public class DaoMonthlyInterest
    {
        private readonly AccessDB _accessDB;

        public DaoMonthlyInterest(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        private const string SelectRuleQuery = @"
            IF OBJECT_ID(N'dbo.monthly_interest_settings', N'U') IS NOT NULL
                SELECT percentage, apply_day FROM dbo.monthly_interest_settings WHERE effective_date = @EffectiveDate;";

        /// <summary>Rule that governs the month of <paramref name="date"/> (configured or default).</summary>
        public async Task<MonthlyInterestRule> GetRuleForMonthAsync(DateTime date)
        {
            var parameters = new[] { MonthParameter("@EffectiveDate", date) };
            DataTable table = await _accessDB.GetTableAsync("monthly_interest_rule", SelectRuleQuery, parameters);
            return ToRule(table);
        }

        /// <summary>Same as <see cref="GetRuleForMonthAsync(DateTime)"/>, inside an open transaction.</summary>
        public async Task<MonthlyInterestRule> GetRuleForMonthAsync(DateTime date, SqlConnection connection, SqlTransaction transaction)
        {
            using var command = new SqlCommand(SelectRuleQuery, connection, transaction);
            command.Parameters.Add(MonthParameter("@EffectiveDate", date));
            using var reader = await command.ExecuteReaderAsync();
            var table = new DataTable();
            table.Load(reader);
            return ToRule(table);
        }

        public async Task<bool> IsMonthAppliedAsync(DateTime date)
        {
            const string query = @"
                IF OBJECT_ID(N'dbo.monthly_interest_runs', N'U') IS NOT NULL
                    SELECT COUNT(1) FROM dbo.monthly_interest_runs WHERE period = @Period
                ELSE
                    SELECT 0;";
            object result = await _accessDB.ExecuteScalarAsync(query, [MonthParameter("@Period", date)]);
            return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
        }

        /// <summary>
        /// Records that the job applied the month. Returns false when it was already recorded
        /// (no second row is created). Without the table it does nothing and returns true.
        /// </summary>
        public async Task<bool> RecordRunAsync(DateTime period, MonthlyInterestRule rule, int affectedRentals, decimal totalAmount)
        {
            const string query = @"
                IF OBJECT_ID(N'dbo.monthly_interest_runs', N'U') IS NULL
                    SELECT 1
                ELSE IF EXISTS (SELECT 1 FROM dbo.monthly_interest_runs WHERE period = @Period)
                    SELECT 0
                ELSE
                BEGIN
                    INSERT INTO dbo.monthly_interest_runs (period, percentage, apply_day, used_default, affected_rentals, total_amount)
                    VALUES (@Period, @Percentage, @ApplyDay, @UsedDefault, @Affected, @Total);
                    SELECT 1;
                END";
            SqlParameter[] parameters =
            [
                MonthParameter("@Period", period),
                new SqlParameter("@Percentage", SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = rule.Percentage },
                new SqlParameter("@ApplyDay", SqlDbType.TinyInt) { Value = (byte)rule.ApplyDay },
                new SqlParameter("@UsedDefault", SqlDbType.Bit) { Value = rule.IsDefault },
                new SqlParameter("@Affected", SqlDbType.Int) { Value = affectedRentals },
                new SqlParameter("@Total", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = totalAmount },
            ];
            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return result != null && result != DBNull.Value && Convert.ToInt32(result) == 1;
        }

        public async Task<DataTable> GetAllSettingsAsync()
        {
            const string query = "SELECT * FROM dbo.monthly_interest_settings ORDER BY effective_date DESC";
            return await _accessDB.GetTableAsync("monthly_interest_settings", query);
        }

        public async Task<DataTable> GetRecentRunsAsync(int take = 24)
        {
            string query = $"SELECT TOP ({take}) * FROM dbo.monthly_interest_runs ORDER BY period DESC";
            return await _accessDB.GetTableAsync("monthly_interest_runs", query);
        }

        public async Task<DataRow?> GetByIdAsync(int id)
        {
            const string query = "SELECT * FROM dbo.monthly_interest_settings WHERE interest_setting_id = @Id";
            DataTable table = await _accessDB.GetTableAsync("monthly_interest_setting", query,
                [new SqlParameter("@Id", SqlDbType.Int) { Value = id }]);
            return table.Rows.Count > 0 ? table.Rows[0] : null;
        }

        public async Task<bool> IsMonthConfiguredAsync(DateTime date)
        {
            const string query = "SELECT COUNT(1) FROM dbo.monthly_interest_settings WHERE effective_date = @EffectiveDate";
            object result = await _accessDB.ExecuteScalarAsync(query, [MonthParameter("@EffectiveDate", date)]);
            return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
        }

        public async Task<int> CreateAsync(DateTime effectiveDate, decimal percentage, int applyDay)
        {
            const string query = @"
                INSERT INTO dbo.monthly_interest_settings (effective_date, percentage, apply_day)
                OUTPUT INSERTED.interest_setting_id
                VALUES (@EffectiveDate, @Percentage, @ApplyDay)";
            SqlParameter[] parameters =
            [
                MonthParameter("@EffectiveDate", effectiveDate),
                new SqlParameter("@Percentage", SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = percentage },
                new SqlParameter("@ApplyDay", SqlDbType.TinyInt) { Value = (byte)applyDay },
            ];
            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToInt32(result);
        }

        public async Task<bool> UpdateAsync(int id, decimal percentage, int applyDay)
        {
            const string query = @"
                UPDATE dbo.monthly_interest_settings
                SET percentage = @Percentage, apply_day = @ApplyDay, updated_at = GETDATE()
                WHERE interest_setting_id = @Id";
            SqlParameter[] parameters =
            [
                new SqlParameter("@Percentage", SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = percentage },
                new SqlParameter("@ApplyDay", SqlDbType.TinyInt) { Value = (byte)applyDay },
                new SqlParameter("@Id", SqlDbType.Int) { Value = id },
            ];
            return await _accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string query = "DELETE FROM dbo.monthly_interest_settings WHERE interest_setting_id = @Id";
            return await _accessDB.ExecuteCommandAsync(query, [new SqlParameter("@Id", SqlDbType.Int) { Value = id }]) > 0;
        }

        private static SqlParameter MonthParameter(string name, DateTime date) =>
            new(name, SqlDbType.Date) { Value = InterestRule.FirstDayOfMonth(date) };

        private static MonthlyInterestRule ToRule(DataTable table)
        {
            if (table.Rows.Count == 0) return InterestRule.Default;
            var row = table.Rows[0];
            return new MonthlyInterestRule(
                Convert.ToDecimal(row["percentage"]),
                Convert.ToInt32(row["apply_day"]),
                false);
        }
    }
}
