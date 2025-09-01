using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.address
{

	public interface IAddressService
	{
		List<Address> GetAddressList();

		List<Address> GetAddressListByClientId(int id);

		public bool CreateAddress(Address address);
	}
}