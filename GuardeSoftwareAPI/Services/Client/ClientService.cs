using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Services.rental;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using GuardeSoftwareAPI.Services.locker;
using GuardeSoftwareAPI.Dtos.Locker;
using GuardeSoftwareAPI.Services.activityLog;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.Common;
using Microsoft.IdentityModel.Tokens;
using GuardeSoftwareAPI.Services.email;
using Quartz.Util;
using GuardeSoftwareAPI.Services.phone;
using GuardeSoftwareAPI.Services.address;

namespace GuardeSoftwareAPI.Services.client
{

    public class ClientService : IClientService
    {
        private readonly DaoClient daoClient;
        private readonly IAddressService addressService;
        private readonly IRentalService rentalService;
        private readonly IRentalAmountHistoryService rentalAmountHistoryService;
        private readonly ILockerService lockerService;
        private readonly IActivityLogService activityLogService;
        private readonly IEmailService emailService;
        private readonly IPhoneService phoneService;
        private readonly AccessDB accessDB;

        public ClientService(AccessDB _accessDB, IRentalService _rentalService, IRentalAmountHistoryService _rentalAmountHistoryService, ILockerService _lockerService, IActivityLogService _activityLogService, IEmailService _emailService, IPhoneService _phoneService, IAddressService _addressService)
        {
            daoClient = new DaoClient(_accessDB);
            addressService = _addressService;
            rentalService = _rentalService;
            rentalAmountHistoryService = _rentalAmountHistoryService;
            lockerService = _lockerService;
            activityLogService = _activityLogService;
            emailService = _emailService;
            phoneService = _phoneService;
            accessDB = _accessDB;
        }

        public async Task<List<Client>> GetClientsList()
        {

            DataTable clientTable = await daoClient.GetClients();
            List<Client> clients = [];

            foreach (DataRow row in clientTable.Rows)
            {
                int clientId = (int)row["client_id"];

                Client client = new()
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

        public async Task<List<Client>> GetClientListById(int id)
        {
            DataTable clientTable = await daoClient.GetClientById(id);
            List<Client> clients = [];

            foreach (DataRow row in clientTable.Rows)
            {
                int clientId = (int)row["client_id"];

                Client client = new()
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
            if (string.IsNullOrWhiteSpace(dto.FirstName)) throw new ArgumentException("FirstName is required.");
            if (string.IsNullOrWhiteSpace(dto.LastName)) throw new ArgumentException("LastName is required.");
            if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than 0.");
            if (dto.LockerIds == null || dto.LockerIds.Count == 0) throw new ArgumentException("At least one lockerId is required.");
            if (dto.LockerIds.Any(id => id <= 0)) throw new ArgumentException("LockerIds must be positive numbers.");
            if (dto.LockerIds.Distinct().Count() != dto.LockerIds.Count) throw new ArgumentException("Duplicate lockerIds are not allowed.");
            if (dto.UserID <= 0) throw new ArgumentException("Invalid UserID.");

            if (!string.IsNullOrEmpty(dto.Dni) && string.IsNullOrWhiteSpace(dto.Dni))
                throw new ArgumentException("DNI cannot be empty or whitespace.", nameof(dto.Dni));

            // if (!string.IsNullOrEmpty(dto.Cuit) && string.IsNullOrWhiteSpace(dto.Cuit))
            //     throw new ArgumentException("CUIT cannot be empty or whitespace.", nameof(dto.Cuit));

            if (dto.RegistrationDate == default) dto.RegistrationDate = DateTime.UtcNow;

            using (var connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {

                        if (dto.Dni != null && await daoClient.ExistsByDniAsync(dto.Dni, connection, transaction))
                        {
                            throw new InvalidOperationException("A client with this DNI already exists.");
                        }

                        if (dto.Cuit != null && !dto.Cuit.IsNullOrEmpty() && await daoClient.ExistsByCuitAsync(dto.Cuit, connection, transaction))
                        {
                            throw new InvalidOperationException("A client with this CUIT already exists.");
                        }

                        Client client = new()
                        {
                            PaymentIdentifier = dto.PaymentIdentifier,
                            FirstName = dto.FirstName?.Trim() ?? string.Empty,
                            LastName = dto.LastName?.Trim() ?? string.Empty,
                            RegistrationDate = dto.RegistrationDate,
                            Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni.Trim(),
                            Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim(),
                            PreferredPaymentMethodId = dto.PreferredPaymentMethodId ?? 0,
                            IvaCondition = string.IsNullOrWhiteSpace(dto.IvaCondition) ? null : dto.IvaCondition.Trim(),
                            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                        };

                        int newId = await daoClient.CreateClientTransactionAsync(client, connection, transaction);

                        Rental rental = new()
                        {
                            ClientId = newId,
                            StartDate = dto.StartDate,
                            ContractedM3 = dto.ContractedM3,
                            MonthsUnpaid = 0

                        };

                        int rentalId = await rentalService.CreateRentalTransactionAsync(rental, connection, transaction);

                        RentalAmountHistory rentalAmountHistory = new()
                        {
                            RentalId = rentalId,
                            Amount = dto.Amount,
                            StartDate = dto.StartDate,
                        };

                        var rentalAmountHistoryId = await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(rentalAmountHistory, connection, transaction);


                        foreach (var lockerId in dto.LockerIds)
                        {
                            if (!await lockerService.IsLockerAvailableAsync(lockerId, connection, transaction))
                                throw new InvalidOperationException($"Locker {lockerId} is already occupied.");
                        }

                        await lockerService.SetRentalTransactionAsync(rentalId, dto.LockerIds, connection, transaction);

                        foreach (string email in dto.Emails)
                        {
                            if (!email.IsNullOrWhiteSpace())
                            {
                                Email emailEntity = new()
                                {
                                    ClientId = newId,
                                    Address = email.Trim(),
                                    Type = ""
                                };
                                await emailService.CreateEmailTransaction(emailEntity, connection, transaction);
                            }
                        }

                        foreach (string phone in dto.Phones)
                        {
                            if (!phone.IsNullOrWhiteSpace())
                            {
                                Phone phoneEntity = new()
                                {
                                    ClientId = newId,
                                    Number = phone.Trim(),
                                    Type = "",
                                    Whatsapp = false
                                };
                                await phoneService.CreatePhoneTransaction(phoneEntity, connection, transaction);
                            }
                        }

                        Address address = new()
                        {
                            ClientId = newId,
                            Street = dto.AddressDto.Street?.Trim() ?? string.Empty,
                            City = dto.AddressDto.City?.Trim() ?? string.Empty,
                            Province = dto.AddressDto.Province?.Trim() ?? string.Empty,
                        };
                        
                        await addressService.CreateAddressTransaction(address, connection, transaction);

                        ActivityLog activityLog = new()
                        {
                            UserId = dto.UserID,
                            LogDate = dto.StartDate,
                            Action = "CREATE",
                            TableName = "clients",
                            RecordId = newId,
                        };

                        await activityLogService.CreateActivityLogTransactionAsync(activityLog, connection, transaction);

                        await transaction.CommitAsync();

                        return newId;
                    }
                    catch
                    {

                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }
        public async Task<GetClientDetailDTO> GetClientDetailByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("Invalid client ID.");

            DataTable clientDetailTable = await daoClient.GetClientDetailByIdAsync(id);

            if (clientDetailTable == null || clientDetailTable.Rows.Count == 0) throw new ArgumentException("No client found with the given ID.");

            DataRow row = clientDetailTable.Rows[0];

            GetClientDetailDTO clientDetail = new()
            {
                // Personal information
                Id = Convert.ToInt32(row["client_id"]),
                PaymentIdentifier = row["payment_identifier"] != DBNull.Value ? Convert.ToDecimal(row["payment_identifier"]) : 0m,
                Name = row["first_name"]?.ToString() ?? string.Empty,
                LastName = row["last_name"]?.ToString() ?? string.Empty,
                City = row["city"]?.ToString() ?? string.Empty,
                Province = row["province"]?.ToString() ?? string.Empty,
                Cuit = row["cuit"]?.ToString() ?? string.Empty,
                Dni = row["dni"]?.ToString() ?? string.Empty,
                RegistrationDate = Convert.ToDateTime(row["registration_date"]),

                // Contact Information
                Email = row["email_address"]?.ToString() ?? string.Empty,
                Phone = row["phone_number"]?.ToString() ?? string.Empty,
                Address = row["street"]?.ToString() ?? string.Empty,

                // Payment & rental Information
                IvaCondition = row["iva_condition"]?.ToString() ?? string.Empty,
                PreferredPaymentMethod = row["preferred_payment_method"]?.ToString() ?? "No especificado",
                IncresePerentage = row["increase_percentage"] != DBNull.Value ? Convert.ToDecimal(row["increase_percentage"]) : 0,
                IncreaseFrequency = row["increase_frequency"] != DBNull.Value ? Convert.ToInt32(row["increase_frequency"]) : 0,
                NextIncreaseDay = row["end_date"] != DBNull.Value ? Convert.ToDateTime(row["end_date"]) : DateTime.MinValue,
                NextPaymentDay = row["next_payment_day"] != DBNull.Value ? Convert.ToDateTime(row["next_payment_day"]) : DateTime.MinValue,
                ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
                Balance = row["balance"] != DBNull.Value ? Convert.ToDecimal(row["balance"]) : 0,
                PaymentStatus = row["payment_status"]?.ToString() ?? "Desconocido",
                RentAmount = row["rent_amount"] != DBNull.Value ? Convert.ToDecimal(row["rent_amount"]) : 0m,

                // Other information
                Notes = row["notes"]?.ToString() ?? string.Empty,
            };

            List<GetLockerClientDetailDTO> lockers = await lockerService.GetLockersByClientIdAsync(id);
            clientDetail.LockersList = lockers;

            return clientDetail;
        }
        
        public async Task<PaginatedResultDto<GetTableClientsDto>> GetClientsTableAsync(GetClientsRequestDto request)
        {
            var (clients, totalCount) = await daoClient.GetTableClientsAsync(request);

            return new PaginatedResultDto<GetTableClientsDto>
            {
                Items = clients,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }
    }
}




    