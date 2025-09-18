using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Address;


namespace GuardeSoftwareAPI.Services.address
{

	public interface IAddressService
	{
		List<Address> GetAddressList();

		List<Address> GetAddressListByClientId(int id);

		public bool CreateAddress(Address address);

		public bool UpdateAddress(int clientId, UpdateAddressDto dto);

    }
}