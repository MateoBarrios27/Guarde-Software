using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoPhone
    {
        private readonly AccessDB accessDB;

        public DaoPhone(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetPhones()
        {
            string query = "SELECT phone_id, client_id, number, type, whatsapp FROM phones"
;
            return await accessDB.GetTableAsync("phones", query);
        }

        public async Task<DataTable> GetPhonesByClientId(int clientId)
        {

            string query = "SELECT phone_id, client_id, number, type, whatsapp FROM phones WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return await accessDB.GetTableAsync("phones", query, parameters);
        }

        public async Task<bool> CreatePhone(Phone phone)
        {
            string query = "INSERT INTO phones (client_id, number, type, whatsapp) VALUES (@client_id, @number, @type, @whatsapp)";
            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@client_id", SqlDbType.Int){Value  = phone.ClientId},
                new SqlParameter("@number", SqlDbType.NVarChar, 20){Value  = phone.Number},
                new SqlParameter("@type", SqlDbType.NVarChar, 50){Value  = phone.Type},
                new SqlParameter("@whatsapp", SqlDbType.Bit){Value  = phone.Whatsapp},
            };
            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
            
        }

        public async Task<bool> DeletePhone(int id)
        {

            string query = "UPDATE phones SET active = 0 WHERE phone_id = @phone_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@phone_id", SqlDbType.Int ) { Value = id},
            };

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
    }
}