using System.ComponentModel.DataAnnotations;

namespace GuardeSoftwareAPI.Dtos.CommunicationTemplate
{
    public class CommunicationTemplateDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        /// <summary>Null for a system template that keeps its original content (the frontend resolves it by SourceKey).</summary>
        public string? Content { get; set; }
        /// <summary>Key of a system template (icbc, inmobiliarias, logisticas…); null for templates created by users.</summary>
        public string? SourceKey { get; set; }
        public int SortOrder { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CommunicationTemplateListDTO
    {
        /// <summary>False while the migration that creates communication_templates is not applied: read-only defaults.</summary>
        public bool StorageReady { get; set; }
        public List<CommunicationTemplateDTO> Templates { get; set; } = new();
    }

    public class SaveCommunicationTemplateDTO
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "El asunto es obligatorio.")]
        [StringLength(300, ErrorMessage = "El asunto admite hasta 300 caracteres.")]
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Template body. Required for user templates. For a system template, null keeps (or restores)
        /// its original content.
        /// </summary>
        public string? Content { get; set; }
    }

    public class ReorderCommunicationTemplatesDTO
    {
        [Required]
        public List<int> TemplateIds { get; set; } = new();
    }
}
