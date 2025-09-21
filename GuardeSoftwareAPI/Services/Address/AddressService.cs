using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Dtos.Address;



namespace GuardeSoftwareAPI.Services.address
{

	public class AddressService : IAddressService
    {
		private readonly DaoAddress daoAddress;

		public AddressService(AccessDB accessDB)
		{
			daoAddress = new  DaoAddress(accessDB);
		}

		public List<Address> GetAddressList() {

			DataTable addressTable = daoAddress.GetAddress();
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

        public List<Address> GetAddressListByClientId(int id)
        {

            DataTable addressTable = daoAddress.GetAddressByClientId(id);
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

		public bool CreateAddress(Address address)
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

            if (daoAddress.CreateAddress(address)) return true;
            else return false;
			
		}

        public bool UpdateAddress(int clientId, UpdateAddressDto dto)
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

            return daoAddress.UpdateAddress(newAddress);
        }

    }
}