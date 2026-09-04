using System.ComponentModel.DataAnnotations;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.MassCommunicationRecipient;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.massCommunicationRecipient
{
    public class MassCommunicationRecipientService : IMassCommunicationRecipientService
    {
        private readonly DaoMassCommunicationRecipient _dao;
        private readonly MassCommunicationRecipientImportParser _importParser = new();

        public MassCommunicationRecipientService(AccessDB accessDB)
        {
            _dao = new DaoMassCommunicationRecipient(accessDB);
        }

        public async Task<List<MassCommunicationRecipientDto>> GetAllAsync()
        {
            var recipients = await _dao.GetActiveAsync();
            return recipients.Select(Map).ToList();
        }

        public async Task<MassCommunicationRecipientDto?> GetByIdAsync(int id)
        {
            ValidateId(id);
            var recipient = await _dao.GetByIdAsync(id);
            return recipient is null ? null : Map(recipient);
        }

        public async Task<MassCommunicationRecipientDto> CreateAsync(UpsertMassCommunicationRecipientDto dto)
        {
            var recipient = NormalizeAndValidate(dto);
            return Map(await _dao.CreateAsync(recipient));
        }

        public async Task<MassCommunicationRecipientDto?> UpdateAsync(int id, UpsertMassCommunicationRecipientDto dto)
        {
            ValidateId(id);
            var recipient = NormalizeAndValidate(dto);
            var updated = await _dao.UpdateAsync(id, recipient);
            return updated is null ? null : Map(updated);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            ValidateId(id);
            return await _dao.DeleteAsync(id);
        }

        public async Task<MassCommunicationRecipientImportResultDto> ImportAsync(
            MassCommunicationRecipientImportRequest request)
        {
            if (request is null)
            {
                throw new ArgumentException("Los datos de importación son obligatorios.");
            }

            string recipientType = Normalize(request.Type) ?? "Inmobiliaria";
            if (recipientType.Length > 100)
            {
                throw new ArgumentException("El tipo o rubro no puede superar los 100 caracteres.");
            }

            if (request.File is null)
            {
                throw new ArgumentException("Seleccioná un archivo CSV, TSV o XLSX.");
            }

            IReadOnlyList<MassCommunicationRecipientImportRecord> parsedRows =
                await _importParser.ParseAsync(request.File);

            var result = new MassCommunicationRecipientImportResultDto
            {
                DryRun = request.DryRun,
                Type = recipientType,
                TotalRows = parsedRows.Count
            };

            var validRows = new List<MassCommunicationRecipientImportRecord>();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var issues = new List<MassCommunicationRecipientImportIssueDto>();

            foreach (MassCommunicationRecipientImportRecord row in parsedRows)
            {
                string? name = Normalize(row.Name);
                string? email = Normalize(row.Email);
                string? phone = Normalize(row.Phone);
                string emailKey = MassCommunicationRecipientImportParser.NormalizeEmail(email);

                if (string.IsNullOrWhiteSpace(email))
                {
                    result.MissingEmailCount++;
                    AddIssue(issues, row.RowNumber, name, email, "Falta el email.");
                    continue;
                }

                if (!new EmailAddressAttribute().IsValid(email))
                {
                    result.InvalidCount++;
                    AddIssue(issues, row.RowNumber, name, email, "El email no tiene un formato válido.");
                    continue;
                }

                if (name?.Length > 150)
                {
                    result.InvalidCount++;
                    AddIssue(issues, row.RowNumber, name, email, "El nombre supera los 150 caracteres.");
                    continue;
                }

                if (phone?.Length > 50)
                {
                    result.InvalidCount++;
                    AddIssue(issues, row.RowNumber, name, email, "El teléfono supera los 50 caracteres.");
                    continue;
                }

                if (!seenEmails.Add(emailKey))
                {
                    result.DuplicateCount++;
                    AddIssue(issues, row.RowNumber, name, email, "Email duplicado dentro del archivo.");
                    continue;
                }

                validRows.Add(new MassCommunicationRecipientImportRecord
                {
                    RowNumber = row.RowNumber,
                    Name = name,
                    Email = email,
                    Phone = phone,
                    EmailKey = emailKey
                });
            }

            result.ValidRows = validRows.Count;

            if (validRows.Count > 0)
            {
                var databaseImport = await _dao.ImportAsync(
                    validRows,
                    recipientType,
                    request.ReactivateInactive,
                    request.DryRun);

                result.NewCount = databaseImport.NewCount;
                result.ExistingActiveCount = databaseImport.ExistingActiveCount;
                result.ExistingInactiveCount = databaseImport.ExistingInactiveCount;
                result.ReactivatedCount = databaseImport.ReactivatedCount;
                result.UpdatedCount = databaseImport.UpdatedCount;
                result.SkippedInactiveCount = databaseImport.SkippedInactiveCount;
                result.ImportedCount = request.DryRun ? 0 : databaseImport.NewCount;
            }

            result.Issues = issues.Take(100).ToList();
            result.HasMoreIssues = issues.Count > result.Issues.Count;
            return result;
        }

        private static void AddIssue(
            List<MassCommunicationRecipientImportIssueDto> issues,
            int rowNumber,
            string? name,
            string? email,
            string reason)
        {
            issues.Add(new MassCommunicationRecipientImportIssueDto
            {
                RowNumber = rowNumber,
                Name = name,
                Email = email,
                Reason = reason
            });
        }

        private static MassCommunicationRecipient NormalizeAndValidate(UpsertMassCommunicationRecipientDto dto)
        {
            if (dto is null)
            {
                throw new ArgumentException("Los datos del receptor son obligatorios.");
            }

            string? name = Normalize(dto.Name);
            string? email = Normalize(dto.Email);
            string? phone = Normalize(dto.Phone);
            string? type = Normalize(dto.Type);

            if (name?.Length > 150)
            {
                throw new ArgumentException("El nombre no puede superar los 150 caracteres.");
            }

            if (email?.Length > 255)
            {
                throw new ArgumentException("El email no puede superar los 255 caracteres.");
            }

            if (email is not null && !new EmailAddressAttribute().IsValid(email))
            {
                throw new ArgumentException("La dirección de email no es válida.");
            }

            if (phone?.Length > 50)
            {
                throw new ArgumentException("El teléfono no puede superar los 50 caracteres.");
            }

            if (type?.Length > 100)
            {
                throw new ArgumentException("El tipo o rubro no puede superar los 100 caracteres.");
            }

            return new MassCommunicationRecipient
            {
                Name = name,
                Email = email,
                Phone = phone,
                Type = type
            };
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static void ValidateId(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("El identificador del receptor no es válido.");
            }
        }

        private static MassCommunicationRecipientDto Map(MassCommunicationRecipient recipient)
        {
            return new MassCommunicationRecipientDto
            {
                Id = recipient.Id,
                Name = recipient.Name,
                Email = recipient.Email,
                Phone = recipient.Phone,
                Type = recipient.Type,
                Active = recipient.Active,
                CreatedAt = recipient.CreatedAt,
                UpdatedAt = recipient.UpdatedAt
            };
        }
    }
}
