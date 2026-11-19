using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    /// <summary>
    /// Communication templates (communication_templates). Every query checks the table exists so the API
    /// keeps answering before the migration Update_Add_CommunicationTemplates.sql is applied.
    /// </summary>
    public class DaoCommunicationTemplate
    {
        private readonly AccessDB _accessDB;

        public DaoCommunicationTemplate(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        public async Task<bool> TableExistsAsync()
        {
            object result = await _accessDB.ExecuteScalarAsync(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.communication_templates', N'U') IS NULL THEN 0 ELSE 1 END");
            return result != null && result != DBNull.Value && Convert.ToInt32(result) == 1;
        }

        public async Task<DataTable> GetAllAsync()
        {
            const string query = @"
                SELECT template_id, name, subject, content, source_key, sort_order, updated_at
                FROM dbo.communication_templates
                ORDER BY sort_order, template_id";
            return await _accessDB.GetTableAsync("communication_templates", query);
        }

        public async Task<DataRow?> GetByIdAsync(int id)
        {
            const string query = @"
                SELECT template_id, name, subject, content, source_key, sort_order, updated_at
                FROM dbo.communication_templates WHERE template_id = @Id";
            DataTable table = await _accessDB.GetTableAsync("communication_template", query, [IdParameter(id)]);
            return table.Rows.Count > 0 ? table.Rows[0] : null;
        }

        /// <summary>Creates the template at the end of the list.</summary>
        public async Task<int> CreateAsync(string name, string subject, string content)
        {
            const string query = @"
                INSERT INTO dbo.communication_templates (name, subject, content, source_key, sort_order)
                OUTPUT INSERTED.template_id
                VALUES (@Name, @Subject, @Content, NULL,
                        (SELECT ISNULL(MAX(sort_order), 0) + 1 FROM dbo.communication_templates))";
            object result = await _accessDB.ExecuteScalarAsync(query,
                [NameParameter(name), SubjectParameter(subject), ContentParameter(content)]);
            return Convert.ToInt32(result);
        }

        public async Task<bool> UpdateAsync(int id, string name, string subject, string? content)
        {
            const string query = @"
                UPDATE dbo.communication_templates
                SET name = @Name, subject = @Subject, content = @Content, updated_at = GETDATE()
                WHERE template_id = @Id";
            return await _accessDB.ExecuteCommandAsync(query,
                [NameParameter(name), SubjectParameter(subject), ContentParameter(content), IdParameter(id)]) > 0;
        }

        /// <summary>Sets sort_order = position (1-based) for each id, in a single statement.</summary>
        public async Task ReorderAsync(IReadOnlyList<int> orderedIds)
        {
            if (orderedIds.Count == 0) return;
            var values = new StringBuilder();
            var parameters = new List<SqlParameter>();
            for (int i = 0; i < orderedIds.Count; i++)
            {
                if (i > 0) values.Append(", ");
                values.Append($"(@Id{i}, {i + 1})");
                parameters.Add(new SqlParameter($"@Id{i}", SqlDbType.Int) { Value = orderedIds[i] });
            }
            string query = $@"
                UPDATE t SET t.sort_order = v.position
                FROM dbo.communication_templates t
                INNER JOIN (VALUES {values}) AS v(template_id, position) ON v.template_id = t.template_id";
            await _accessDB.ExecuteCommandAsync(query, parameters.ToArray());
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string query = "DELETE FROM dbo.communication_templates WHERE template_id = @Id";
            return await _accessDB.ExecuteCommandAsync(query, [IdParameter(id)]) > 0;
        }

        private static SqlParameter IdParameter(int id) => new("@Id", SqlDbType.Int) { Value = id };
        private static SqlParameter NameParameter(string name) => new("@Name", SqlDbType.NVarChar, 150) { Value = name };
        private static SqlParameter SubjectParameter(string subject) => new("@Subject", SqlDbType.NVarChar, 300) { Value = subject };
        private static SqlParameter ContentParameter(string? content) =>
            new("@Content", SqlDbType.NVarChar, -1) { Value = (object?)content ?? DBNull.Value };
    }
}
