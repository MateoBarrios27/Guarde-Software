using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;


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
    }
}