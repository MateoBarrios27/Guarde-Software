using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Email;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.email
{

	public interface IEmailService
	{
		Task<List<Email>> GetEmailsList();
		Task<List<Email>> GetEmailListByClientId(int id);
		Task<Email> CreateEmail(Email email);
	    Task<bool> DeleteEmail(int id);
		Task<bool> UpdateEmail(int clientId, UpdateEmailDto dto);
		Task<Email> CreateEmailTransaction(Email email, SqlConnection sqlConnection, SqlTransaction sqlTransaction);

    }
}