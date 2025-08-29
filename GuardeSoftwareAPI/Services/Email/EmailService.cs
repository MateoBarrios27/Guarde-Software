using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;

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
    }
}