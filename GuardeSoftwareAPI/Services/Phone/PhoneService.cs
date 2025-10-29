using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.phone
{
    public class PhoneService : IPhoneService
    {
        private readonly DaoPhone _daoPhone;

        public PhoneService(AccessDB accessDB)
        {
            _daoPhone = new DaoPhone(accessDB);
        }

        public async Task<List<Phone>> GetPhonesList()
        {

            DataTable phonesTable = await _daoPhone.GetPhones();
            List<Phone> phones = [];

            foreach (DataRow row in phonesTable.Rows)
            {
                int idPhone = (int)row["phone_id"];

                Phone phone = new Phone
                {
                    Id = idPhone,
                    ClientId = row["client_id"] != DBNull.Value
                    ? (int)row["client_id"] : 0,
                    Number = row["number"]?.ToString() ?? string.Empty,
                    Type = row["type"]?.ToString() ?? string.Empty,
                    Whatsapp = row["whatsapp"] != DBNull.Value
                    ? (bool)row["whatsapp"] : false,

                };
                phones.Add(phone);
            }
            return phones;
        }

        public async Task<List<Phone>> GetPhoneListByClientId(int id)
        {

            DataTable phonesTable = await _daoPhone.GetPhonesByClientId(id);
            List<Phone> phones = new List<Phone>();

            foreach (DataRow row in phonesTable.Rows)
            {
                int idPhone = (int)row["phone_id"];

                Phone phone = new Phone
                {
                    Id = idPhone,
                    ClientId = row["client_id"] != DBNull.Value
                    ? (int)row["client_id"] : 0,
                    Number = row["number"]?.ToString() ?? string.Empty,
                    Type = row["type"]?.ToString() ?? string.Empty,
                    Whatsapp = row["whatsapp"] != DBNull.Value
                    ? (bool)row["whatsapp"] : false,

                };
                phones.Add(phone);
            }
            return phones;
        }

        public async Task<Phone> CreatePhoneTransaction(Phone phone, SqlConnection connection, SqlTransaction transaction)
        {
            if (phone == null)
                throw new ArgumentNullException(nameof(phone));
            if (string.IsNullOrWhiteSpace(phone.Number))
                throw new ArgumentException("Phone Number is required.");

            return await _daoPhone.CreatePhoneTransaction(phone, connection, transaction);
        }

        public async Task<bool> DeletePhonesByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
         {
             if (clientId <= 0) throw new ArgumentException("Invalid Client ID.");
             int rowsAffected = await _daoPhone.DeletePhonesByClientIdTransactionAsync(clientId, connection, transaction);
             return rowsAffected > 0; // Opcional
         }
    }
}