using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Dtos.Email;

namespace GuardeSoftwareAPI.Services.email
{

	public class EmailService : IEmailService
	{
		private readonly DaoEmail _daoEmail;

		public EmailService(AccessDB accessDB)
		{
			_daoEmail = new DaoEmail(accessDB);
		}

		public List<Email> GetEmailsList()
		{

			DataTable emailsTable = _daoEmail.GetEmails();
			List<Email> emails = new List<Email>();

			foreach (DataRow row in emailsTable.Rows)
			{
				int idEmail = (int)row["email_id"];

				Email email = new Email
				{
					Id = idEmail,
					ClientId = row["client_id"] != DBNull.Value
					? (int)row["client_id"] : 0,
					Address = row["address"]?.ToString() ?? string.Empty,
					Type = row["type"]?.ToString() ?? string.Empty,

				};
				emails.Add(email);
			}
			return emails;
		}

        public List<Email> GetEmailListByClientId(int id)
        {

            DataTable emailsTable = _daoEmail.GetEmailsByClientId(id);
            List<Email> emails = new List<Email>();

            foreach (DataRow row in emailsTable.Rows)
            {
                int idEmail = (int)row["email_id"];

                Email email = new Email
                {
                    Id = idEmail,
                    ClientId = row["client_id"] != DBNull.Value
                    ? (int)row["client_id"] : 0,
                    Address = row["address"]?.ToString() ?? string.Empty,
                    Type = row["type"]?.ToString() ?? string.Empty,

                };
                emails.Add(email);
            }
            return emails;
        }

		public bool CreateEmail(Email email)
		{
            if (email == null)
                throw new ArgumentNullException(nameof(email));

            if (email.ClientId <= 0)
                throw new ArgumentException("Invalid ClientId.");

            if(string.IsNullOrWhiteSpace(email.Address))
                throw new ArgumentException("Email Address is required.");

            if (string.IsNullOrWhiteSpace(email.Type))
                throw new ArgumentException("Email type is required.");

            if (_daoEmail.CreateEmail(email)) return true;
            else return false;
        }

        public bool DeleteEmail(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid Email Id.");

            if (_daoEmail.DeleteEmail(id)) return true;
            else return false;
        }

        public bool UpdateEmail(int clientId, UpdateEmailDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (clientId <= 0)
                throw new ArgumentException("Invalid ClientId.");

            if (dto.Id <= 0)
                throw new ArgumentException("Invalid Email Id.");

            if (string.IsNullOrWhiteSpace(dto.Address))
                throw new ArgumentException("Email Address is required.");

            if (string.IsNullOrWhiteSpace(dto.Type))
                throw new ArgumentException("Email type is required.");

            var Email = new Email
            {
                Id = dto.Id,
                ClientId = clientId,
                Address = dto.Address,
                Type = dto.Type,
            };

            return _daoEmail.UpdateEmail(Email);
        }
    }
}