using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;

namespace GuardeSoftwareAPI.Services.client
{

    public class ClientService : IClientService
    {
        private readonly DaoClient daoClient;

        public ClientService(AccessDB accessDB)
        {
            daoClient = new DaoClient(accessDB);
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
    }
}



    