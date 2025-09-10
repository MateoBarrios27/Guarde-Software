using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Services.rental;

namespace GuardeSoftwareAPI.Services.client
{

    public class ClientService : IClientService
    {
        private readonly DaoClient daoClient;

        private readonly IRentalService rentalService;

        public ClientService(AccessDB accessDB, IRentalService _rentalService)
        {
            daoClient = new DaoClient(accessDB);
            this.rentalService = _rentalService;
        }

        public List<Client> GetClientsList()
        {

            DataTable clientTable = daoClient.GetClients();
            List<Client> clients = new List<Client>();

            foreach (DataRow row in clientTable.Rows)
            {
                int clientId = (int)row["client_id"];

                Client client = new Client
                {
                    Id = clientId,
                    PaymentIdentifier = row["payment_identifier"] != DBNull.Value ? Convert.ToDecimal(row["payment_identifier"]) : 0m,
                    FirstName = row["first_name"]?.ToString() ?? string.Empty,
                    LastName = row["last_name"]?.ToString() ?? string.Empty,
                    RegistrationDate = (DateTime)row["registration_date"],
                    Notes = row["notes"]?.ToString() ?? string.Empty,
                    Dni = row["dni"]?.ToString() ?? string.Empty,
                    Cuit = row["cuit"]?.ToString() ?? string.Empty,
                    PreferredPaymentMethodId = row["preferred_payment_method_id"] != DBNull.Value ? (int)row["preferred_payment_method_id"] : 0,
                };
                clients.Add(client);
            }
            return clients;
        }

        public List<Client> GetClientListById(int  id)
        {
            DataTable clientTable = daoClient.GetClientById(id);
            List<Client> clients = new List<Client>();

            foreach (DataRow row in clientTable.Rows)
            {
                int clientId = (int)row["client_id"];

                Client client = new Client
                {
                    Id = clientId,
                    PaymentIdentifier = row["payment_identifier"] != DBNull.Value ? Convert.ToDecimal(row["payment_identifier"]) : 0m,
                    FirstName = row["first_name"]?.ToString() ?? string.Empty,
                    LastName = row["last_name"]?.ToString() ?? string.Empty,
                    RegistrationDate = (DateTime)row["registration_date"],
                    Notes = row["notes"]?.ToString() ?? string.Empty,
                    Dni = row["dni"]?.ToString() ?? string.Empty,
                    Cuit = row["cuit"]?.ToString() ?? string.Empty,
                    PreferredPaymentMethodId = row["preferred_payment_method_id"] != DBNull.Value ? (int)row["preferred_payment_method_id"] : 0,
                };
                clients.Add(client);
            }
            return clients;
        }

        public async Task<int> CreateClientAsync(CreateClientDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.FirstName)) throw new ArgumentException("FirstName es requerido.");
            if (string.IsNullOrWhiteSpace(dto.LastName)) throw new ArgumentException("LastName es requerido.");

            if (dto.RegistrationDate == default) dto.RegistrationDate = DateTime.UtcNow;

            var client = new Client
            {  
                PaymentIdentifier = dto.PaymentIdentifier,
                FirstName = dto.FirstName?.Trim(),
                LastName = dto.LastName?.Trim(),
                RegistrationDate = dto.RegistrationDate,
                Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni.Trim(),
                Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim(),
                PreferredPaymentMethodId = dto.PreferredPaymentMethodId ?? 0, 
                IvaCondition = string.IsNullOrWhiteSpace(dto.IvaCondition) ? null : dto.IvaCondition.Trim(),
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            };

            int newId = await daoClient.CreateClientAsync(client);

            var rental = new Rental
            {
                ClientId = newId,
                StartDate = dto.StartDate,
                ContractedM3 = dto.ContractedM3,
            };

            int rentalId = await rentalService.CreateRentalAsync(rental);

            var rentalAmountHistory = new RentalAmountHistory
            {
                RentalId = rentalId,
                Amount = dto.Amount,
                StartDate = dto.StartDate,
            };

            return newId;
        }

    }
}



    