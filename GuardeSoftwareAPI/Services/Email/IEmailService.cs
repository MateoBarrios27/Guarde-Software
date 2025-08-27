using System;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Services.email
{

	public interface IEmailService
	{
		List<Email> GetEmailsList();

		List<Email> GetEmailListByClientId(int id);

	}
}