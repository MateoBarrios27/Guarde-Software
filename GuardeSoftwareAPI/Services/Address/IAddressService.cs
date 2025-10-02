using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Address;


namespace GuardeSoftwareAPI.Services.address
{

	public interface IAddressService
	{
		Task<List<Address>> GetAddressList();

		Task<List<Address>> GetAddressListByClientId(int id);

		Task<Address> CreateAddress(Address address);

		Task<bool> UpdateAddress(int clientId, UpdateAddressDto dto);

    }
}