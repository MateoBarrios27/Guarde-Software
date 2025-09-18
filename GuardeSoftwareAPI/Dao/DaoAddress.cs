using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoAddress
    {
        private readonly AccessDB accessDB;

        public DaoAddress(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetAddress()
        {
            string query = "SELECT address_id, client_id, street, city, province FROM addresses";

            return accessDB.GetTable("addresses", query);
        }

        public DataTable GetAddressByClientId(int cliendId) {

            string query = "SELECT address_id, client_id, street, city, province FROM addresses WHERE client_id = @client_id ";

            SqlParameter[] parameters = new SqlParameter[] {

               new SqlParameter("@client_id", SqlDbType.Int ) {Value =  cliendId},
            };

            return accessDB.GetTable("addresses",query, parameters);
        }

        public bool CreateAddress(Address address) {

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int){Value = address.ClientId },
                new SqlParameter("@street", SqlDbType.VarChar){Value = address.Street },
                new SqlParameter("@city", SqlDbType.VarChar){Value = address.City },
                new SqlParameter("@province", SqlDbType.VarChar){Value = address.Province },
            };

            string query = "INSERT INTO addresses(client_id, street, city, province)VALUES(@client_id, @street, @city, @province)";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

        public bool UpdateAddress(int clientId, Address newAddress)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int){Value = clientId},
                new SqlParameter("@street", SqlDbType.VarChar){Value = newAddress.Street},
                new SqlParameter("@city", SqlDbType.VarChar){Value = newAddress.City},
                new SqlParameter("@province", SqlDbType.VarChar){Value = (object?)newAddress.Province ?? DBNull.Value},
                new SqlParameter("@address_id", SqlDbType.Int){Value = newAddress.Id}
            };

            string query = "UPDATE addresses SET street = @street, city = @city, province = @province WHERE address_id = @address_id AND client_id = @client_id";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

    }
}
