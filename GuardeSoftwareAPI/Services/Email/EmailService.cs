using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Dtos.Email;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.email
{

    public class EmailService : IEmailService
    {
        private readonly DaoEmail _daoEmail;

        public EmailService(AccessDB accessDB)
        {
            _daoEmail = new DaoEmail(accessDB);
        }

        public async Task<List<Email>> GetEmailsList()
        {

            DataTable emailsTable = await _daoEmail.GetEmails();
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

        public async Task<List<Email>> GetEmailListByClientId(int id)
        {

            DataTable emailsTable = await _daoEmail.GetEmailsByClientId(id);
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

        public async Task<Email> CreateEmail(Email email)
        {
            if (email == null)
                throw new ArgumentNullException(nameof(email));

            if (email.ClientId <= 0)
                throw new ArgumentException("Invalid ClientId.");

            if (string.IsNullOrWhiteSpace(email.Address))
                throw new ArgumentException("Email Address is required.");

            return await _daoEmail.CreateEmail(email);
        }

        public async Task<bool> DeleteEmail(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid Email Id.");

            if (await _daoEmail.DeleteEmail(id)) return true;
            else return false;
        }

        public async Task<bool> UpdateEmail(int clientId, UpdateEmailDto dto)
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

            return await _daoEmail.UpdateEmail(Email);
        }


        public async Task<Email> CreateEmailTransaction(Email email, SqlConnection connection, SqlTransaction transaction)
        {
            if (email == null)
                throw new ArgumentNullException(nameof(email));

            if (email.ClientId <= 0)
                throw new ArgumentException("Invalid ClientId.");

            if (string.IsNullOrWhiteSpace(email.Address))
                throw new ArgumentException("Email Address is required.");

            return await _daoEmail.CreateEmailTransaction(email, connection, transaction);
        }
        
        public async Task<bool> DeleteEmailsByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
         {
             if (clientId <= 0) throw new ArgumentException("Invalid Client ID.");
             int rowsAffected = await _daoEmail.DeleteEmailsByClientIdTransactionAsync(clientId, connection, transaction);
             return rowsAffected > 0; // Opcional
         }
    }
}