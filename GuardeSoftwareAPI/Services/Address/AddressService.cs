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
            daoAddress = new DaoAddress(accessDB);
        }

        public async Task<List<Address>> GetAddressList() 
        {
            DataTable addressTable = await daoAddress.GetAddress();
            List<Address> addresses = new List<Address>();

            foreach (DataRow row in addressTable.Rows)
            { 
                int idAdress = (int)row["address_id"];

                string street = row["street"]?.ToString() ?? string.Empty;
                string city = row["city"]?.ToString() ?? string.Empty;
                string province = row["province"]?.ToString() ?? string.Empty;

                string fullAddress = street;
                if (!string.IsNullOrWhiteSpace(city)) fullAddress += $", {city}";
                if (!string.IsNullOrWhiteSpace(province)) fullAddress += $", {province}";

                Address adress = new Address
                {
                    Id = idAdress,
                    ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
                    Street = fullAddress.TrimEnd(',', ' '), // Devolvemos todo unificado acá
                    City = "", 
                    Province = ""
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

                string street = row["street"]?.ToString() ?? string.Empty;
                string city = row["city"]?.ToString() ?? string.Empty;
                string province = row["province"]?.ToString() ?? string.Empty;

                string fullAddress = street;
                if (!string.IsNullOrWhiteSpace(city)) fullAddress += $", {city}";
                if (!string.IsNullOrWhiteSpace(province)) fullAddress += $", {province}";

                Address address = new Address
                {
                    Id = addressId,
                    ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
                    Street = fullAddress.TrimEnd(',', ' '),
                    City = "",
                    Province = ""
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
                throw new ArgumentException("Street (Dirección) is required.");

            // ELIMINADA LA VALIDACIÓN DE CITY OBLIGATORIA
            address.City = string.IsNullOrWhiteSpace(address.City) ? "" : address.City.Trim();
            address.Province = string.IsNullOrWhiteSpace(address.Province) ? "" : address.Province.Trim();

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
                throw new ArgumentException("Street (Dirección) is required.");

            // ELIMINADA LA VALIDACIÓN DE CITY OBLIGATORIA

            var newAddress = new Address
            {
                Id = dto.Id,
                ClientId = clientId,
                Street = dto.Street.Trim(),
                City = "", // Forzamos vacío
                Province = "" // Forzamos vacío
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
                throw new ArgumentException("Street (Dirección) is required.");

            // ELIMINADA LA VALIDACIÓN DE CITY OBLIGATORIA
            address.City = string.IsNullOrWhiteSpace(address.City) ? "" : address.City.Trim();
            address.Province = string.IsNullOrWhiteSpace(address.Province) ? "" : address.Province.Trim();

            return await daoAddress.CreateAddressTransaction(address, sqlConnection, transaction);
        }

        public async Task<int> DeleteAddressByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid Client ID.");
            int rowsAffected = await daoAddress.DeleteAddressByClientIdTransactionAsync(clientId, connection, transaction);
            return rowsAffected; 
        }
    }
}