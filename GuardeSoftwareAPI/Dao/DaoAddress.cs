using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoAddress
    {
        private readonly AccessDB accessDB;

        public DaoAddress(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetAddress()
        {
            string query = "SELECT address_id, client_id, street, city, province FROM addresses";

            return await accessDB.GetTableAsync("addresses", query);
        }

        public async Task<DataTable> GetAddressByClientId(int clientId) {

            string query = "SELECT address_id, client_id, street, city, province FROM addresses WHERE client_id = @client_id ";

            SqlParameter[] parameters = [

               new SqlParameter("@client_id", SqlDbType.Int ) {Value =  clientId},
            ];

            return await accessDB.GetTableAsync("addresses",query, parameters);
        }

        public async Task<Address> CreateAddress(Address address) {

            SqlParameter[] parameters =
            [
                new SqlParameter("@client_id", SqlDbType.Int){Value = address.ClientId },
                new SqlParameter("@street", SqlDbType.VarChar){Value = address.Street },
                new SqlParameter("@city", SqlDbType.VarChar){Value = address.City },
                new SqlParameter("@province", SqlDbType.VarChar){Value = address.Province },
            ];

            string query = "INSERT INTO addresses (client_id, street, city, province) VALUES (@client_id, @street, @city, @province); SELECT SCOPE_IDENTITY();";

            object newId = await accessDB.ExecuteScalarAsync(query, parameters);

            if (newId != null && newId != DBNull.Value)
            {
                //Assign the newly generated ID to the address object
                address.Id = Convert.ToInt32(newId);
            }

            return address;
        }

        public async Task<bool> UpdateAddress(Address newAddress)
        {
            SqlParameter[] parameters =
            [
                new SqlParameter("@client_id", SqlDbType.Int){Value = newAddress.ClientId},
                new SqlParameter("@street", SqlDbType.VarChar){Value = newAddress.Street},
                new SqlParameter("@city", SqlDbType.VarChar){Value = newAddress.City},
                new SqlParameter("@province", SqlDbType.VarChar){Value = (object?)newAddress.Province ?? DBNull.Value},
                new SqlParameter("@address_id", SqlDbType.Int){Value = newAddress.Id}
            ];

            string query = "UPDATE addresses SET street = @street, city = @city, province = @province WHERE address_id = @address_id AND client_id = @client_id";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<Address> CreateAddressTransaction(Address address, SqlConnection sqlConnection, SqlTransaction transaction)
        {
            string query = "INSERT INTO addresses (client_id, street, city, province) VALUES (@client_id, @street, @city, @province); SELECT SCOPE_IDENTITY();";

            SqlParameter[] parameters =
            [
                new SqlParameter("@client_id", SqlDbType.Int){Value = address.ClientId },
                new SqlParameter("@street", SqlDbType.VarChar){Value = address.Street },
                new SqlParameter("@city", SqlDbType.VarChar){Value = address.City },
                new SqlParameter("@province", SqlDbType.VarChar){Value = (object?)address.Province ?? DBNull.Value },
            ];

            using (SqlCommand command = new SqlCommand(query, sqlConnection, transaction))
            {
                command.Parameters.AddRange(parameters);
                object newId = await command.ExecuteScalarAsync();

                if (newId != null && newId != DBNull.Value)
                {
                    //Assign the newly generated ID to the address object
                    address.Id = Convert.ToInt32(newId);
                }
            }

            return address;
        }

        public async Task<int> DeleteAddressByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            // Asumiendo que solo hay UNA dirección por cliente
            string query = "DELETE FROM addresses WHERE client_id = @client_id";
            SqlParameter[] parameters = { new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId } };

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                return await command.ExecuteNonQueryAsync();
            }
        }

    }
}
