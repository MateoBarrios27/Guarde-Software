using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.CommunicationTemplate;

namespace GuardeSoftwareAPI.Services.communicationTemplate
{
    public interface ICommunicationTemplateService
    {
        Task<CommunicationTemplateListDTO> GetAllAsync();
        Task<CommunicationTemplateDTO> CreateAsync(SaveCommunicationTemplateDTO dto);
        Task<CommunicationTemplateDTO?> UpdateAsync(int id, SaveCommunicationTemplateDTO dto);
        Task<CommunicationTemplateListDTO> ReorderAsync(IReadOnlyList<int> orderedIds);
        Task<bool> DeleteAsync(int id);
    }

    /// <summary>
    /// Comunicaciones → Plantillas. The six system templates are seeded by the migration with their
    /// source_key and no content: the frontend owns their original HTML (assets/email-templates and the
    /// template catalog). Editing their content stores an override; sending a null content restores the
    /// original. Without the table the list is read-only and returns the system templates.
    /// </summary>
    public class CommunicationTemplateService : ICommunicationTemplateService
    {
        public const int MaxContentLength = 2_000_000;
        private const string StorageNotReadyMessage =
            "Las plantillas todavía no se pueden editar: falta aplicar la actualización de la base de datos.";

        /// <summary>System templates, in their original order. Must match the migration seed.</summary>
        public static readonly IReadOnlyList<(string Key, string Name, string Subject)> SystemTemplates =
        [
            ("icbc", "ICBC", "Beneficio especial ICBC para clientes"),
            ("inmobiliarias", "Inmobiliarias", "PUBLICIDAD | Una solución de guardado para tu inmobiliaria y tus clientes"),
            ("logisticas", "Logísticas", "Tu logística, con más espacio."),
            ("laboratorios", "Laboratorios", "PUBLICIDAD | Espacio flexible para materiales y equipamiento de tu laboratorio"),
            ("visitadores-medicos", "Visitadores médicos", "PUBLICIDAD | Más espacio para organizar tu material de trabajo"),
            ("icbc-2", "ICBC 2", "Beneficios ICBC para nuestros clientes"),
        ];

        private readonly DaoCommunicationTemplate _dao;

        public CommunicationTemplateService(AccessDB accessDB)
        {
            _dao = new DaoCommunicationTemplate(accessDB);
        }

        public async Task<CommunicationTemplateListDTO> GetAllAsync()
        {
            if (!await _dao.TableExistsAsync())
            {
                return new CommunicationTemplateListDTO
                {
                    StorageReady = false,
                    Templates = SystemTemplates.Select((t, i) => new CommunicationTemplateDTO
                    {
                        Id = -(i + 1),
                        Name = t.Name,
                        Subject = t.Subject,
                        SourceKey = t.Key,
                        SortOrder = i + 1,
                    }).ToList(),
                };
            }

            var templates = new List<CommunicationTemplateDTO>();
            foreach (DataRow row in (await _dao.GetAllAsync()).Rows)
                templates.Add(Map(row));
            return new CommunicationTemplateListDTO { StorageReady = true, Templates = templates };
        }

        public async Task<CommunicationTemplateDTO> CreateAsync(SaveCommunicationTemplateDTO dto)
        {
            await EnsureStorageAsync();
            var (name, subject) = Normalize(dto);
            string content = dto.Content ?? string.Empty;
            ValidateContent(content, required: true);

            int id = await _dao.CreateAsync(name, subject, content);
            DataRow row = await _dao.GetByIdAsync(id)
                ?? throw new InvalidOperationException("No se pudo leer la plantilla creada.");
            return Map(row);
        }

        public async Task<CommunicationTemplateDTO?> UpdateAsync(int id, SaveCommunicationTemplateDTO dto)
        {
            await EnsureStorageAsync();
            DataRow? existing = await _dao.GetByIdAsync(id);
            if (existing == null) return null;

            var (name, subject) = Normalize(dto);
            bool isSystem = existing["source_key"] != DBNull.Value;
            string? content = dto.Content;
            if (isSystem)
            {
                // Null (or empty) keeps / restores the original HTML of the system template.
                if (string.IsNullOrWhiteSpace(content)) content = null;
                else ValidateContent(content, required: false);
            }
            else
            {
                ValidateContent(content ?? string.Empty, required: true);
            }

            await _dao.UpdateAsync(id, name, subject, content);
            DataRow? updated = await _dao.GetByIdAsync(id);
            return updated == null ? null : Map(updated);
        }

        public async Task<CommunicationTemplateListDTO> ReorderAsync(IReadOnlyList<int> orderedIds)
        {
            await EnsureStorageAsync();
            var current = (await _dao.GetAllAsync()).Rows.Cast<DataRow>()
                .Select(row => Convert.ToInt32(row["template_id"])).ToList();

            if (orderedIds.Count != current.Count
                || orderedIds.Distinct().Count() != orderedIds.Count
                || !orderedIds.All(current.Contains))
            {
                throw new InvalidOperationException(
                    "La lista de plantillas cambió mientras la ordenabas. Volvé a cargarla e intentá de nuevo.");
            }

            await _dao.ReorderAsync(orderedIds);
            return await GetAllAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            await EnsureStorageAsync();
            return await _dao.DeleteAsync(id);
        }

        private async Task EnsureStorageAsync()
        {
            if (!await _dao.TableExistsAsync())
                throw new InvalidOperationException(StorageNotReadyMessage);
        }

        private static (string Name, string Subject) Normalize(SaveCommunicationTemplateDTO dto)
        {
            string name = (dto.Name ?? string.Empty).Trim();
            string subject = (dto.Subject ?? string.Empty).Trim();
            if (name.Length == 0) throw new ArgumentException("El nombre es obligatorio.");
            if (subject.Length == 0) throw new ArgumentException("El asunto es obligatorio.");
            if (name.Length > 150) throw new ArgumentException("El nombre admite hasta 150 caracteres.");
            if (subject.Length > 300) throw new ArgumentException("El asunto admite hasta 300 caracteres.");
            return (name, subject);
        }

        private static void ValidateContent(string content, bool required)
        {
            if (required && string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("El contenido es obligatorio.");
            if (content.Length > MaxContentLength)
                throw new ArgumentException("El contenido es demasiado grande para guardarlo como plantilla.");
        }

        private static CommunicationTemplateDTO Map(DataRow row) => new()
        {
            Id = Convert.ToInt32(row["template_id"]),
            Name = Convert.ToString(row["name"]) ?? string.Empty,
            Subject = Convert.ToString(row["subject"]) ?? string.Empty,
            Content = row["content"] == DBNull.Value ? null : Convert.ToString(row["content"]),
            SourceKey = row["source_key"] == DBNull.Value ? null : Convert.ToString(row["source_key"]),
            SortOrder = Convert.ToInt32(row["sort_order"]),
            UpdatedAt = row["updated_at"] == DBNull.Value ? null : Convert.ToDateTime(row["updated_at"]),
        };
    }
}
