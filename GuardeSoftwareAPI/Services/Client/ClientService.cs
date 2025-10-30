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
using System.Text.Json;
using GuardeSoftwareAPI.Services.accountMovement;

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
        private readonly ILogger<ClientService> _logger;
        private readonly IAccountMovementService accountMovementService;
        private readonly AccessDB accessDB;

        public ClientService(AccessDB _accessDB, ILogger<ClientService> logger, IAccountMovementService _accountMovementService, IRentalService _rentalService, IRentalAmountHistoryService _rentalAmountHistoryService, ILockerService _lockerService, IActivityLogService _activityLogService, IEmailService _emailService, IPhoneService _phoneService, IAddressService _addressService)
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
            accountMovementService = _accountMovementService;
            _logger = logger;
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

            if (dto.RegistrationDate == default) dto.RegistrationDate = DateTime.UtcNow.Date;
            if (dto.StartDate == default) dto.StartDate = DateTime.UtcNow.Date; 

            using (var connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // set PaymentIdentifier
                        if (dto.PaymentIdentifier == null || dto.PaymentIdentifier.Value <= 0)
                        {
                            // 1. Obtains the current maximum identifier
                            decimal maxIdentifier = await daoClient.GetMaxPaymentIdentifierAsync(connection, transaction);
                            
                            // 2. Asign a new identifier incrementing the max by 0.01
                            dto.PaymentIdentifier = maxIdentifier + 0.01m; 
                            
                            // NOTA: Si el primer cliente debe ser 0.1, considera esto en el DAO o aquí.
                            // Si el máximo es 0.00, el nuevo será 0.01. Si es 0.14, el nuevo será 0.15.
                            // Ajusta el incremento (0.01m) según tus necesidades de formato (ej: 1m si son enteros).
                        }

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

                        DateTime? priceLockDate = null;
                        if (dto.PrepaidMonths >= 6)
                        {
                            // Calculamos la fecha fin del bloqueo sumando los meses PREPAGOS a la fecha de INICIO del alquiler
                            // Usamos AddMonths para manejar correctamente fin de mes, años bisiestos, etc.
                            priceLockDate = dto.StartDate.AddMonths(dto.PrepaidMonths);
                            _logger.LogInformation($"Cliente {newId} tiene {dto.PrepaidMonths} meses prepagos. Precio bloqueado hasta {priceLockDate:yyyy-MM-dd}");
                        }

                        Rental rental = new()
                        {
                            ClientId = newId,
                            StartDate = dto.StartDate,
                            ContractedM3 = dto.ContractedM3 ?? 0m,
                            MonthsUnpaid = 0,
                            PriceLockEndDate = priceLockDate
                        };

                        int rentalId = await rentalService.CreateRentalTransactionAsync(rental, connection, transaction);

                        if (dto.PrepaidMonths > 0 && dto.Amount > 0)
                        {
                            decimal totalCreditAmount = dto.PrepaidMonths * dto.Amount;
                            AccountMovement creditMovement = new AccountMovement
                            {
                                RentalId = rentalId,
                                MovementDate = dto.StartDate,
                                MovementType = "CREDITO",
                                Concept = $"Pago adelantado x{dto.PrepaidMonths} {(dto.PrepaidMonths == 1 ? "mes" : "meses")}",
                                Amount = totalCreditAmount,
                                PaymentId = null
                            };
                            await accountMovementService.CreateAccountMovementTransactionAsync(creditMovement, connection, transaction);
                            _logger.LogInformation($"Crédito inicial de {totalCreditAmount:C} registrado para rental {rentalId} ({dto.PrepaidMonths} meses).");
                        }

                        RentalAmountHistory rentalAmountHistory = new()
                        {
                            RentalId = rentalId,
                            Amount = dto.Amount,
                            StartDate = dto.StartDate,
                        };

                        var rentalAmountHistoryId = await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(rentalAmountHistory, connection, transaction);


                        if (dto.LockerIds != null && dto.LockerIds.Count != 0) {
                            foreach (var lockerId in dto.LockerIds) {
                                if (!await lockerService.IsLockerAvailableAsync(lockerId, connection, transaction))
                                    throw new InvalidOperationException($"Locker {lockerId} is already occupied.");
                            }
                            await lockerService.SetRentalTransactionAsync(rentalId, dto.LockerIds, connection, transaction);
                        }

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
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Error en CreateClientAsync. Transacción revertida.");
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
                Address = row["street"]?.ToString() ?? string.Empty,
                // Las propiedades 'Email' y 'Phone' se cargarán a continuación

                // Payment & rental Information
                IvaCondition = row["iva_condition"]?.ToString() ?? string.Empty,
                PreferredPaymentMethod = row["preferred_payment_method"]?.ToString() ?? "No especificado",
                
                // CORREGÍ EL TYPO: Tu DTO dice 'IncresePerentage', lo cambié a 'IncreasePercentage'
                IncreasePercentage = row["increase_percentage"] != DBNull.Value ? Convert.ToDecimal(row["increase_percentage"]) : 0, 
                IncreaseFrequency = row["increase_frequency"] != DBNull.Value ? Convert.ToInt32(row["increase_frequency"]) : 0,
                
                NextIncreaseDay = row["end_date"] != DBNull.Value ? Convert.ToDateTime(row["end_date"]) : DateTime.MinValue,
                NextPaymentDay = row["next_payment_day"] != DBNull.Value ? Convert.ToDateTime(row["next_payment_day"]) : DateTime.MinValue,
                ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
                Balance = row["balance"] != DBNull.Value ? Convert.ToDecimal(row["balance"]) : 0,
                PaymentStatus = row["payment_status"]?.ToString() ?? "Desconocido",
                RentAmount = row["rent_amount"] != DBNull.Value ? Convert.ToDecimal(row["rent_amount"]) : 0m,

                // Other information
                Notes = row["notes"]?.ToString() ?? string.Empty, // Esto está bien
            };

            // --- INICIO DE LA CORRECCIÓN ---

            // 1. Cargar Lockers (esto ya estaba bien)
            List<GetLockerClientDetailDTO> lockers = await lockerService.GetLockersByClientIdAsync(id);
            clientDetail.LockersList = lockers;

            // 2. Cargar Emails usando el servicio inyectado
            var emailEntities = await emailService.GetEmailListByClientId(id);
            // Convertimos la List<Email> (entidad) en un string[]
            clientDetail.Email = emailEntities.Select(e => e.Address).ToArray();

            // 3. Cargar Teléfonos usando el servicio inyectado
            // (Asumo que tu IPhoneService tiene este método, igual que EmailService)
            var phoneEntities = await phoneService.GetPhoneListByClientId(id); 
            // Convertimos la List<Phone> (entidad) en un string[]
            clientDetail.Phone = phoneEntities.Select(p => p.Number).ToArray();

            // --- FIN DE LA CORRECCIÓN ---

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

        public async Task<List<string>> GetClientRecipientNamesAsync()
        {

            return await daoClient.GetActiveClientNamesAsync();
        }

        public async Task<List<string>> SearchClientNamesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<string>();
            }
            return await daoClient.SearchActiveClientNamesAsync(query);
        }
        
        public async Task<bool> UpdateClientAsync(int id, CreateClientDTO dto)
{
    if (id <= 0) throw new ArgumentException("ID de cliente inválido.");
    if (dto == null) throw new ArgumentNullException(nameof(dto));
    // ... (otras validaciones básicas)

    using (var connection = accessDB.GetConnectionClose())
    {
        await connection.OpenAsync();
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                var existingClient = await daoClient.GetClientByIdTransactionAsync(id, connection, transaction);
                if (existingClient == null) return false;

                // string oldClientState = JsonSerializer.Serialize(existingClient); // Opcional para log

                if (!string.IsNullOrWhiteSpace(dto.Dni) && await daoClient.ExistsByDniAsync(dto.Dni, id, connection, transaction))
                    throw new InvalidOperationException("Ya existe otro cliente con este DNI.");
                if (!string.IsNullOrWhiteSpace(dto.Cuit) && await daoClient.ExistsByCuitAsync(dto.Cuit, id, connection, transaction))
                     throw new InvalidOperationException("Ya existe otro cliente con este CUIT.");

                Client clientToUpdate = new Client { /* ... mapeo como antes ... */
                    Id = id,
                    PaymentIdentifier = dto.PaymentIdentifier,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    RegistrationDate = existingClient.RegistrationDate, // No cambiar
                    Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni.Trim(),
                    Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim(),
                    PreferredPaymentMethodId = dto.PreferredPaymentMethodId ?? existingClient.PreferredPaymentMethodId,
                    IvaCondition = string.IsNullOrWhiteSpace(dto.IvaCondition) ? existingClient.IvaCondition : dto.IvaCondition.Trim(),
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? existingClient.Notes : dto.Notes.Trim(),
                 };

                if (!await daoClient.UpdateClientTransactionAsync(clientToUpdate, connection, transaction))
                    throw new Exception("No se pudo actualizar la información principal del cliente.");

                // Usar los MÉTODOS DE SERVICIO para borrar y crear
                await emailService.DeleteEmailsByClientIdTransactionAsync(id, connection, transaction);
                if (dto.Emails != null) {
                    foreach (string emailAddr in dto.Emails.Where(e => !string.IsNullOrWhiteSpace(e))) {
                        await emailService.CreateEmailTransaction(new Email { ClientId = id, Address = emailAddr.Trim(), Type = "" }, connection, transaction);
                    }
                }

                await phoneService.DeletePhonesByClientIdTransactionAsync(id, connection, transaction);
                if (dto.Phones != null) {
                    foreach (string phoneNum in dto.Phones.Where(p => !string.IsNullOrWhiteSpace(p))) {
                        await phoneService.CreatePhoneTransaction(new Phone { ClientId = id, Number = phoneNum.Trim(), Type = "", Whatsapp = false }, connection, transaction);
                    }
                }

                await addressService.DeleteAddressByClientIdTransactionAsync(id, connection, transaction);
                 if (dto.AddressDto != null && !string.IsNullOrWhiteSpace(dto.AddressDto.Street) && !string.IsNullOrWhiteSpace(dto.AddressDto.City)) {
                    await addressService.CreateAddressTransaction(new Address { ClientId = id, Street = dto.AddressDto.Street.Trim(), City = dto.AddressDto.City.Trim(), Province = string.IsNullOrWhiteSpace(dto.AddressDto.Province) ? null : dto.AddressDto.Province.Trim() }, connection, transaction);
                 }


                // Lógica de Rental/Lockers/Amount usando MÉTODOS DE SERVICIO
                var currentRental = await rentalService.GetRentalByClientIdTransactionAsync(id, connection, transaction);
                if (currentRental != null)
                {
                    var lastAmountHistory = await rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(currentRental.Id, connection, transaction);
                    if (lastAmountHistory != null && dto.Amount != lastAmountHistory.Amount)
                    {
                        await rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(lastAmountHistory.Id, currentRental.Id, dto.Amount, DateTime.UtcNow, connection, transaction);
                    }

                    var currentLockerIds = await lockerService.GetLockerIdsByRentalIdTransactionAsync(currentRental.Id, connection, transaction);
                    var newLockerIds = dto.LockerIds ?? new List<int>();
                    var lockersToRemove = currentLockerIds.Except(newLockerIds).ToList();
                    var lockersToAdd = newLockerIds.Except(currentLockerIds).ToList();

                    if (lockersToRemove.Any()) {
                        await lockerService.UnassignLockersFromRentalTransactionAsync(lockersToRemove, connection, transaction);
                    }
                    if (lockersToAdd.Any()) {
                         foreach(var lockerIdToAdd in lockersToAdd) {
                              if (!await lockerService.IsLockerAvailableAsync(lockerIdToAdd, connection, transaction)) {
                                  throw new InvalidOperationException($"El locker {lockerIdToAdd} ya no está disponible.");
                              }
                         }
                        await lockerService.AssignLockersToRentalTransactionAsync(currentRental.Id, lockersToAdd, connection, transaction);
                    }

                    // Recalcular M3 si hubo cambios en lockers
                    if (lockersToAdd.Any() || lockersToRemove.Any()) {
                        decimal newContractedM3 = await lockerService.CalculateTotalM3ForLockersAsync(newLockerIds, connection, transaction);
                        await rentalService.UpdateContractedM3TransactionAsync(currentRental.Id, newContractedM3, connection, transaction);
                    }
                } else if (dto.LockerIds != null && dto.LockerIds.Any()) {
                     // Lógica para crear un nuevo rental si no existía y se asignan lockers?
                     Console.WriteLine($"Advertencia: Se asignaron lockers al cliente {id} pero no tiene un rental activo.");
                }

                // Lógica para Régimen de Aumento (si aplica)

                ActivityLog activityLog = new ActivityLog { /* ... mapeo como antes ... */
                    UserId = dto.UserID,
                    LogDate = DateTime.UtcNow,
                    Action = "UPDATE",
                    TableName = "clients",
                    RecordId = id,
                    // OldValue = oldClientState,
                    NewValue = JsonSerializer.Serialize(dto)
                 };
                await activityLogService.CreateActivityLogTransactionAsync(activityLog, connection, transaction);

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
    }
}




    