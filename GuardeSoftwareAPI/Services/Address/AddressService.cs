using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Dtos.Address;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;



namespace GuardeSoftwareAPI.Services.address
{

	public class AddressService : IAddressService
    {
		private readonly DaoAddress daoAddress;

		public AddressService(AccessDB accessDB)
		{
			daoAddress = new  DaoAddress(accessDB);
		}

		public async Task<List<Address>> GetAddressList() {

			DataTable addressTable = await daoAddress.GetAddress();
			List<Address> addresses = new List<Address>();

			foreach (DataRow row in addressTable.Rows)
			{ 
				int idAdress = (int)row["address_id"];

				Address adress = new Address
				{
					Id = idAdress,
					ClientId = row["client_id"] != DBNull.Value
					? (int)row["client_id"] : 0,
					Street = row["street"]?.ToString() ?? string.Empty,
					City = row["city"]?.ToString() ?? string.Empty,
					Province = row["province"]?.ToString() ?? string.Empty,

				};
				addresses.Add(adress);
            }
			return addresses;
		}

        public async Task<List<Address>> GetAddressListByClientId(int id)
        {

            DataTable addressTable = await daoAddress.GetAddressByClientId(id);
            List<Address> addresses = new List<Address>();

            foreach (DataRow row in addressTable.Rows)
            {
                int addressId = (int)row["address_id"];

                Address address = new Address
                {
                    Id = addressId,
                    ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
                    Street = row["street"]?.ToString() ?? string.Empty,
                    City = row["city"]?.ToString() ?? string.Empty,
                    Province = row["province"]?.ToString() ?? string.Empty,
                };
                addresses.Add(address);
            }
            return addresses;
        }

		public async Task<Address> CreateAddress(Address address)
		{
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.ClientId <= 0)
                throw new ArgumentException("Invalid ClientId.");

            if (string.IsNullOrWhiteSpace(address.Street))
                throw new ArgumentException("Street is required.");

            if (string.IsNullOrWhiteSpace(address.City))
                throw new ArgumentException("City is required.");

            address.Province = string.IsNullOrWhiteSpace(address.Province) ? null : address.Province.Trim();

            return await daoAddress.CreateAddress(address);
			
		}

        public async Task<bool> UpdateAddress(int clientId, UpdateAddressDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (clientId <= 0)
                throw new ArgumentException("Invalid ClientId.");

            if (dto.Id <= 0)
                throw new ArgumentException("Invalid Address Id.");

            if (string.IsNullOrWhiteSpace(dto.Street))
                throw new ArgumentException("Street is required.");

            if (string.IsNullOrWhiteSpace(dto.City))
                throw new ArgumentException("City is required.");

            var newAddress = new Address
            {
                Id = dto.Id,
                ClientId = clientId,
                Street = dto.Street.Trim(),
                City = dto.City.Trim(),
                Province = string.IsNullOrWhiteSpace(dto.Province) ? null : dto.Province.Trim()
            };

            return await daoAddress.UpdateAddress(newAddress);
        }

        public async Task<Address> CreateAddressTransaction(Address address, SqlConnection sqlConnection, SqlTransaction transaction)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.ClientId <= 0)
                throw new ArgumentException("Invalid ClientId.");

            if (string.IsNullOrWhiteSpace(address.Street))
                throw new ArgumentException("Street is required.");

            if (string.IsNullOrWhiteSpace(address.City))
                throw new ArgumentException("City is required.");

            address.Province = string.IsNullOrWhiteSpace(address.Province) ? null : address.Province.Trim();

            return await daoAddress.CreateAddressTransaction(address, sqlConnection, transaction);
        }

        public async Task<bool> DeleteAddressByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid Client ID.");
            int rowsAffected = await daoAddress.DeleteAddressByClientIdTransactionAsync(clientId, connection, transaction);
            return rowsAffected > 0; // Opcional: podrías no necesitar devolver bool si no te importa si existía o no
        }

    }
}