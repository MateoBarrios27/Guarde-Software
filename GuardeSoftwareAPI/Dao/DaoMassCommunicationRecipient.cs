using System.Data;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoMassCommunicationRecipient
    {
        private readonly AccessDB _accessDB;

        public DaoMassCommunicationRecipient(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        public async Task<List<MassCommunicationRecipient>> GetActiveAsync()
        {
            const string query = @"
                SELECT recipient_id, name, email, phone, recipient_type, active, created_at, updated_at
                FROM mass_communication_recipients
                WHERE active = 1
                ORDER BY
                    CASE WHEN NULLIF(LTRIM(RTRIM(name)), '') IS NULL THEN 1 ELSE 0 END,
                    name,
                    recipient_id";

            var table = await _accessDB.GetTableAsync("MassCommunicationRecipients", query);
            return table.Rows.Cast<DataRow>().Select(Map).ToList();
        }

        public async Task<MassCommunicationRecipient?> GetByIdAsync(int id, bool includeInactive = false)
        {
            string query = @"
                SELECT recipient_id, name, email, phone, recipient_type, active, created_at, updated_at
                FROM mass_communication_recipients
                WHERE recipient_id = @Id";

            if (!includeInactive)
            {
                query += " AND active = 1";
            }

            var table = await _accessDB.GetTableAsync(
                "MassCommunicationRecipient",
                query,
                new[] { new SqlParameter("@Id", SqlDbType.Int) { Value = id } });

            return table.Rows.Count == 0 ? null : Map(table.Rows[0]);
        }

        public async Task<MassCommunicationRecipient> CreateAsync(MassCommunicationRecipient recipient)
        {
            const string query = @"
                INSERT INTO mass_communication_recipients (name, email, phone, recipient_type)
                OUTPUT INSERTED.recipient_id
                VALUES (@Name, @Email, @Phone, @Type)";

            var parameters = BuildParameters(recipient);
            object result = await _accessDB.ExecuteScalarAsync(query, parameters);
            recipient.Id = Convert.ToInt32(result);

            return (await GetByIdAsync(recipient.Id, includeInactive: true))
                ?? throw new InvalidOperationException("No se pudo recuperar el receptor creado.");
        }

        public async Task<MassCommunicationRecipient?> UpdateAsync(int id, MassCommunicationRecipient recipient)
        {
            const string query = @"
                UPDATE mass_communication_recipients
                SET name = @Name,
                    email = @Email,
                    phone = @Phone,
                    recipient_type = @Type,
                    updated_at = GETDATE()
                WHERE recipient_id = @Id AND active = 1";

            var parameters = BuildParameters(recipient, id);
            int rows = await _accessDB.ExecuteCommandAsync(query, parameters);
            if (rows == 0) return null;

            return await GetByIdAsync(id, includeInactive: true);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string query = @"
                UPDATE mass_communication_recipients
                SET active = 0,
                    updated_at = GETDATE()
                WHERE recipient_id = @Id AND active = 1";

            return await _accessDB.ExecuteCommandAsync(
                query,
                new[] { new SqlParameter("@Id", SqlDbType.Int) { Value = id } }) > 0;
        }

        private static SqlParameter[] BuildParameters(MassCommunicationRecipient recipient, int? id = null)
        {
            var parameters = new List<SqlParameter>
            {
                new("@Name", SqlDbType.NVarChar, 150) { Value = (object?)recipient.Name ?? DBNull.Value },
                new("@Email", SqlDbType.NVarChar, 255) { Value = (object?)recipient.Email ?? DBNull.Value },
                new("@Phone", SqlDbType.NVarChar, 50) { Value = (object?)recipient.Phone ?? DBNull.Value },
                new("@Type", SqlDbType.NVarChar, 100) { Value = (object?)recipient.Type ?? DBNull.Value }
            };

            if (id.HasValue)
            {
                parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id.Value });
            }

            return parameters.ToArray();
        }

        private static MassCommunicationRecipient Map(DataRow row)
        {
            return new MassCommunicationRecipient
            {
                Id = Convert.ToInt32(row["recipient_id"]),
                Name = row["name"] is DBNull ? null : row["name"]?.ToString(),
                Email = row["email"] is DBNull ? null : row["email"]?.ToString(),
                Phone = row["phone"] is DBNull ? null : row["phone"]?.ToString(),
                Type = row["recipient_type"] is DBNull ? null : row["recipient_type"]?.ToString(),
                Active = row["active"] is not DBNull && Convert.ToBoolean(row["active"]),
                CreatedAt = row["created_at"] is DBNull
                    ? DateTime.MinValue
                    : Convert.ToDateTime(row["created_at"]),
                UpdatedAt = row["updated_at"] is DBNull
                    ? null
                    : Convert.ToDateTime(row["updated_at"])
            };
        }
    }
}
