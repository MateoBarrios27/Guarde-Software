using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoEmail
    {
        private readonly AccessDB accessDB;

        public DaoEmail(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetEmails()
        {
            string query = "SELECT email_id, client_id, address, type FROM emails WHERE active = 1";

            return await accessDB.GetTableAsync("emails", query);
        }

        public async Task<DataTable> GetEmailsByClientId(int clientId) {

            string query = "SELECT email_id, client_id, address, type FROM emails WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value =  clientId},
            };
            
            return await accessDB.GetTableAsync("emails", query, parameters);
        }

        public async Task<Email> CreateEmail(Email email) {

            SqlParameter[] parameters = [

                new SqlParameter("@client_id",SqlDbType.Int){Value = email.ClientId},
                new SqlParameter("@address",SqlDbType.VarChar,150){Value = email.Address},
                new SqlParameter("@type",SqlDbType.VarChar,50){Value = (object?)email.Type ?? DBNull.Value},
            ];

            string query = "INSERT INTO emails(client_id, address, type)VALUES(@client_id, @address, @type); SELECT SCOPE_IDENTITY()";

            object newId = await accessDB.ExecuteScalarAsync(query, parameters);

            if (newId != null && newId != DBNull.Value)
            {
                //Assign the newly generated ID to the email object
                email.Id = Convert.ToInt32(newId);
            }

            return email;
        }

        public async Task<bool> DeleteEmail(int id)
        {

            string query = "UPDATE emails SET active = 0 WHERE email_id = @email_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@email_id", SqlDbType.Int ) { Value = id},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<bool> UpdateEmail(Email email) {

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@email_id", SqlDbType.Int) { Value = email.Id},
                new SqlParameter("@client_id",SqlDbType.Int){Value = email.ClientId},
                new SqlParameter("@address",SqlDbType.VarChar,150){Value = email.Address},
                new SqlParameter("@type",SqlDbType.VarChar,50){Value = (object?)email.Type ?? DBNull.Value},
            };

            string query = "UPDATE emails SET address = @address, type = @type WHERE email_id = @email_id AND client_id = @client_id";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
    }
}
