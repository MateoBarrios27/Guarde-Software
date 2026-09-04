using System.Data;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.massCommunicationRecipient;
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

    public async Task<MassCommunicationRecipientImportDatabaseResult> ImportAsync(
        IReadOnlyList<MassCommunicationRecipientImportRecord> records,
        string recipientType,
        bool reactivateInactive,
        bool dryRun)
    {
        var result = new MassCommunicationRecipientImportDatabaseResult();
        if (records.Count == 0) return result;

        using SqlConnection connection = _accessDB.GetConnectionClose();
        await connection.OpenAsync();

        SqlTransaction? transaction = null;
        try
        {
            if (!dryRun)
            {
                transaction = (SqlTransaction)await connection.BeginTransactionAsync();
                await AcquireImportLockAsync(connection, transaction);
            }

            await CreateImportTableAsync(connection, transaction);
            await BulkCopyImportRowsAsync(connection, transaction, records, recipientType);

            result.Matches = await GetExistingMatchesAsync(connection, transaction);
            result.ExistingActiveCount = result.Matches.Values.Count(match => match.Active);
            result.ExistingInactiveCount = result.Matches.Values.Count(match => !match.Active);
            result.NewCount = records.Count - result.Matches.Count;
            result.UpdatedCount = result.Matches.Count;
            result.ReactivatedCount = reactivateInactive
                ? result.Matches.Values.Count(match => !match.Active)
                : 0;
            result.SkippedInactiveCount = reactivateInactive
                ? 0
                : result.Matches.Values.Count(match => !match.Active);

            if (!dryRun)
            {
                await UpdateExistingAsync(connection, transaction!, reactivateInactive);
                await InsertNewAsync(connection, transaction!, recipientType);
                await transaction!.CommitAsync();
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Preserve the original database error.
                }
            }

            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private static async Task AcquireImportLockAsync(SqlConnection connection, SqlTransaction transaction)
    {
        const string query = @"
            DECLARE @LockResult INT;
            EXEC @LockResult = sp_getapplock
                @Resource = N'MassCommunicationRecipientsImport',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 10000;
            IF @LockResult < 0
                THROW 51001, 'No se pudo obtener el bloqueo para importar receptores.', 1;";

        using var command = new SqlCommand(query, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreateImportTableAsync(SqlConnection connection, SqlTransaction? transaction)
    {
        const string query = @"
            CREATE TABLE #MassRecipientImport (
                row_number INT NOT NULL,
                name NVARCHAR(150) NULL,
                email NVARCHAR(255) NOT NULL,
                email_key NVARCHAR(255) NOT NULL,
                phone NVARCHAR(50) NULL,
                recipient_type NVARCHAR(100) NOT NULL
            );";

        using var command = new SqlCommand(query, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task BulkCopyImportRowsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        IReadOnlyList<MassCommunicationRecipientImportRecord> records,
        string recipientType)
    {
        var table = new DataTable();
        table.Columns.Add("row_number", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Columns.Add("email", typeof(string));
        table.Columns.Add("email_key", typeof(string));
        table.Columns.Add("phone", typeof(string));
        table.Columns.Add("recipient_type", typeof(string));

        foreach (MassCommunicationRecipientImportRecord record in records)
        {
            table.Rows.Add(
                record.RowNumber,
                (object?)record.Name ?? DBNull.Value,
                record.Email ?? string.Empty,
                record.EmailKey,
                (object?)record.Phone ?? DBNull.Value,
                recipientType);
        }

        using var bulkCopy = transaction is null
            ? new SqlBulkCopy(connection)
            : new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
        bulkCopy.DestinationTableName = "#MassRecipientImport";
        bulkCopy.ColumnMappings.Add("row_number", "row_number");
        bulkCopy.ColumnMappings.Add("name", "name");
        bulkCopy.ColumnMappings.Add("email", "email");
        bulkCopy.ColumnMappings.Add("email_key", "email_key");
        bulkCopy.ColumnMappings.Add("phone", "phone");
        bulkCopy.ColumnMappings.Add("recipient_type", "recipient_type");
        await bulkCopy.WriteToServerAsync(table);
    }

    private static async Task<Dictionary<string, MassCommunicationRecipientImportDatabaseMatch>> GetExistingMatchesAsync(
        SqlConnection connection,
        SqlTransaction? transaction)
    {
        const string query = @"
            WITH ExistingRecipients AS (
                SELECT
                    i.email_key,
                    r.recipient_id,
                    r.active,
                    ROW_NUMBER() OVER (
                        PARTITION BY i.email_key
                        ORDER BY CASE WHEN r.active = 1 THEN 0 ELSE 1 END, r.recipient_id
                    ) AS row_number
                FROM #MassRecipientImport i
                INNER JOIN mass_communication_recipients r
                    ON LOWER(LTRIM(RTRIM(r.email))) = i.email_key
            )
            SELECT email_key, recipient_id, active
            FROM ExistingRecipients
            WHERE row_number = 1;";

        using var command = new SqlCommand(query, connection, transaction);
        using SqlDataReader reader = await command.ExecuteReaderAsync();
        var matches = new Dictionary<string, MassCommunicationRecipientImportDatabaseMatch>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync())
        {
            string emailKey = reader.GetString(0);
            matches[emailKey] = new MassCommunicationRecipientImportDatabaseMatch
            {
                RecipientId = reader.GetInt32(1),
                Active = reader.GetBoolean(2)
            };
        }

        return matches;
    }

    private static async Task UpdateExistingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        bool reactivateInactive)
    {
        const string query = @"
            WITH ExistingRecipients AS (
                SELECT
                    r.recipient_id,
                    i.name AS import_name,
                    i.phone AS import_phone,
                    i.recipient_type,
                    ROW_NUMBER() OVER (
                        PARTITION BY i.email_key
                        ORDER BY CASE WHEN r.active = 1 THEN 0 ELSE 1 END, r.recipient_id
                    ) AS row_number
                FROM #MassRecipientImport i
                INNER JOIN mass_communication_recipients r
                    ON LOWER(LTRIM(RTRIM(r.email))) = i.email_key
            )
            UPDATE r
            SET name = COALESCE(NULLIF(e.import_name, N''), r.name),
                phone = COALESCE(NULLIF(e.import_phone, N''), r.phone),
                recipient_type = e.recipient_type,
                active = CASE WHEN @ReactivateInactive = 1 THEN 1 ELSE r.active END,
                updated_at = GETDATE()
            FROM mass_communication_recipients r
            INNER JOIN ExistingRecipients e
                ON e.recipient_id = r.recipient_id
               AND e.row_number = 1;";

        using var command = new SqlCommand(query, connection, transaction);
        command.Parameters.Add(new SqlParameter("@ReactivateInactive", SqlDbType.Bit)
        {
            Value = reactivateInactive
        });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertNewAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string recipientType)
    {
        const string query = @"
            INSERT INTO mass_communication_recipients (name, email, phone, recipient_type)
            SELECT i.name, i.email, i.phone, @RecipientType
            FROM #MassRecipientImport i
            WHERE NOT EXISTS (
                SELECT 1
                FROM mass_communication_recipients r WITH (UPDLOCK, HOLDLOCK)
                WHERE LOWER(LTRIM(RTRIM(r.email))) = i.email_key
            );";

        using var command = new SqlCommand(query, connection, transaction);
        command.Parameters.Add(new SqlParameter("@RecipientType", SqlDbType.NVarChar, 100)
        {
            Value = recipientType
        });
        await command.ExecuteNonQueryAsync();
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

    public sealed class MassCommunicationRecipientImportDatabaseResult
    {
        public Dictionary<string, MassCommunicationRecipientImportDatabaseMatch> Matches { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int ExistingActiveCount { get; set; }
        public int ExistingInactiveCount { get; set; }
        public int NewCount { get; set; }
        public int ReactivatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedInactiveCount { get; set; }
    }

    public sealed class MassCommunicationRecipientImportDatabaseMatch
    {
        public int RecipientId { get; set; }
        public bool Active { get; set; }
    }
}
