using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Address;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Services.address
{

	public interface IAddressService
	{
		Task<List<Address>> GetAddressList();
		Task<List<Address>> GetAddressListByClientId(int id);
		Task<Address> CreateAddress(Address address);
		Task<bool> UpdateAddress(int clientId, UpdateAddressDto dto);
		Task<Address> CreateAddressTransaction(Address address,SqlConnection sqlConnection, SqlTransaction transaction);
        Task<bool> DeleteAddressByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction);
    }
}