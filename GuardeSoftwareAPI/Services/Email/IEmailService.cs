using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Email;

namespace GuardeSoftwareAPI.Services.email
{

	public interface IEmailService
	{
		List<Email> GetEmailsList();

		List<Email> GetEmailListByClientId(int id);

		public bool CreateEmail(Email email);

        public bool DeleteEmail(int id);

		public bool UpdateEmail(int clientId,UpdateEmailDto dto);

    }
}