using System.ComponentModel.DataAnnotations;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.MassCommunicationRecipient;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.massCommunicationRecipient
{
    public class MassCommunicationRecipientService : IMassCommunicationRecipientService
    {
        private readonly DaoMassCommunicationRecipient _dao;

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

        private static MassCommunicationRecipient NormalizeAndValidate(UpsertMassCommunicationRecipientDto dto)
        {
            if (dto is null)
            {
                throw new ArgumentException("Los datos del receptor son obligatorios.");
            }

            string? name = Normalize(dto.Name);
            string? email = Normalize(dto.Email);
            string? phone = Normalize(dto.Phone);

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

            return new MassCommunicationRecipient
            {
                Name = name,
                Email = email,
                Phone = phone
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
                Active = recipient.Active,
                CreatedAt = recipient.CreatedAt,
                UpdatedAt = recipient.UpdatedAt
            };
        }
    }
}
