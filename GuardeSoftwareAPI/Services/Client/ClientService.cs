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
using System.Globalization;
using Quartz;
using GuardeSoftwareAPI.Dtos.RentalSpaceRequest;

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
        private readonly DaoRentalSpaceRequest _daoRentalSpaceRequest;
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
            _daoRentalSpaceRequest = new DaoRentalSpaceRequest(_accessDB);
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
            ArgumentNullException.ThrowIfNull(dto);
            if (string.IsNullOrWhiteSpace(dto.FirstName)) throw new ArgumentException("FirstName is required.");
            if (string.IsNullOrWhiteSpace(dto.LastName)) throw new ArgumentException("LastName is required.");
            if (dto.Amount < 0) throw new ArgumentException("Amount must be greater than 0.");
            // if (dto.LockerIds == null || dto.LockerIds.Count == 0) throw new ArgumentException("At least one lockerId is required.");
            if (dto.LockerIds.Any(id => id <= 0)) throw new ArgumentException("LockerIds must be positive numbers.");
            if (dto.LockerIds.Distinct().Count() != dto.LockerIds.Count) throw new ArgumentException("Duplicate lockerIds are not allowed.");
            if (dto.UserID <= 0) throw new ArgumentException("Invalid UserID.");

            if (!string.IsNullOrEmpty(dto.Dni) && string.IsNullOrWhiteSpace(dto.Dni))
                throw new ArgumentException("DNI cannot be empty or whitespace.", nameof(dto.Dni));

            

            if (dto.IsLegacyClient)
            {
                if (dto.StartDate == default) throw new ArgumentException("Legacy start date is required.");
                if (!dto.LegacyInitialAmount.HasValue || dto.LegacyInitialAmount < 0) throw new ArgumentException("Legacy initial amount is required.");
                if (!dto.LegacyNextIncreaseDate.HasValue) throw new ArgumentException("Legacy next increase date is required.");
            }
            
            // Asign dates if not legacy
            DateTime startDate = dto.IsLegacyClient ? dto.StartDate : DateTime.UtcNow.Date;
            DateTime registrationDate = dto.IsLegacyClient ? dto.RegistrationDate : DateTime.UtcNow.Date;
            var today = DateTime.UtcNow.Date;
            decimal calculatedTotalM3 = 0;
            
            if (dto.SpaceRequests != null && dto.SpaceRequests.Count != 0)
            {
                calculatedTotalM3 = dto.SpaceRequests.Sum(r => r.M3 * r.Quantity);
            }
            else 
            {
                // Fallback por si acaso viene ContractedM3 directo
                calculatedTotalM3 = dto.ContractedM3 ?? 0m;
            }

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
                            FirstName = dto.FirstName.Trim(),
                            LastName = dto.LastName.Trim(),
                            RegistrationDate = registrationDate,
                            Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni.Trim(),
                            Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim(),
                            PreferredPaymentMethodId = dto.PreferredPaymentMethodId,
                            IvaCondition = string.IsNullOrWhiteSpace(dto.IvaCondition) ? null : dto.IvaCondition.Trim(),
                            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                            BillingTypeId = dto.BillingTypeId,
                            IncreaseFrequencyMonths = dto.IsLegacy6MonthPromo ? 6 : 4,
                            InitialAmount = dto.IsLegacyClient ? dto.LegacyInitialAmount : dto.Amount // Guardar monto inicial
                        };
                        int newClientId = await daoClient.CreateClientTransactionAsync(client, connection, transaction);

                        DateTime? priceLockDate = null;
                        if (dto.PrepaidMonths > 0)
                        {
                            priceLockDate = startDate.AddMonths(dto.PrepaidMonths);
                        }

                        DateTime nextIncreaseAnchorDate;
                        int frequency = dto.IsLegacy6MonthPromo ? 6 : 4;

                        if (dto.IsLegacyClient && dto.LegacyNextIncreaseDate.HasValue)
                        {
                            nextIncreaseAnchorDate = dto.LegacyNextIncreaseDate.Value.Date;
                        }
                        else
                        {
                            var firstAnniversary = startDate.AddMonths(frequency - 1); 
                            nextIncreaseAnchorDate = new DateTime(firstAnniversary.Year, firstAnniversary.Month, 1);
                        }

                        // 4. Crear Rental
                        Rental rental = new()
                        {
                            ClientId = newClientId,
                            StartDate = startDate,
                            ContractedM3 = calculatedTotalM3,
                            MonthsUnpaid = 0,
                            PriceLockEndDate = priceLockDate,
                            IncreaseAnchorDate = nextIncreaseAnchorDate,
                            OccupiedSpaces = dto.OccupiedSpaces,
                        };
                        int rentalId = await rentalService.CreateRentalTransactionAsync(rental, connection, transaction);

                        if (dto.SpaceRequests != null && dto.SpaceRequests.Count != 0)
                        {
                            foreach (var req in dto.SpaceRequests)
                            {
                                var spaceRequest = new RentalSpaceRequest
                                {
                                    RentalId = rentalId,
                                    WarehouseId = req.WarehouseId,
                                    Quantity = req.Quantity,
                                    M3 = req.M3
                                };
                                await _daoRentalSpaceRequest.CreateRequestTransactionAsync(spaceRequest, connection, transaction);
                            }
                        }

                        // 5. Crear Historial de Monto(s)
                        if (dto.IsLegacyClient)
                        {
                            decimal initialAmount = dto.LegacyInitialAmount ?? dto.Amount;
                            
                            // 5a. Crear historial "inicial" (puede ser antiguo)
                            await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(new RentalAmountHistory
                            {
                                RentalId = rentalId,
                                Amount = initialAmount,
                                StartDate = dto.StartDate, 
                                // La fecha fin es un día antes del próximo aumento (o null si el aumento ya pasó)
                                EndDate = (dto.LegacyNextIncreaseDate.HasValue && dto.LegacyNextIncreaseDate.Value > dto.StartDate) 
                                            ? dto.LegacyNextIncreaseDate.Value.AddDays(-1) 
                                            : (DateTime?)null
                            }, connection, transaction);

                            // 5b. Si el monto actual es DIFERENTE al inicial, crear el registro "actual"
                            if (dto.Amount != initialAmount && dto.LegacyNextIncreaseDate.HasValue && dto.LegacyNextIncreaseDate.Value.Date <= today.Date)
                            {
                                await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(new RentalAmountHistory
                                {
                                    RentalId = rentalId,
                                    Amount = dto.Amount, // El monto actual
                                    StartDate = dto.LegacyNextIncreaseDate.Value.Date
                                }, connection, transaction);
                            }
                        }
                        else
                        {
                            // Cliente Nuevo: solo tiene un historial de monto
                            await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(new RentalAmountHistory
                            {
                                RentalId = rentalId,
                                Amount = dto.Amount,
                                StartDate = startDate
                            }, connection, transaction);
                        }


                        // 6. Lógica Débito/Crédito Inicial (sin cambios)
                        if (dto.IsLegacyClient)
                        {
                            if (dto.PrepaidMonths > 0 && dto.Amount > 0)
                            {
                                // Legacy CON prepago: Crear CRÉDITO
                                decimal totalCreditAmount = dto.PrepaidMonths * dto.Amount; // Crédito basado en el monto ACTUAL
                                await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
                                {
                                    RentalId = rentalId,
                                    MovementDate = startDate,
                                    MovementType = "CREDITO",
                                    Concept = $"Crédito inicial por {dto.PrepaidMonths} {(dto.PrepaidMonths == 1 ? "mes" : "meses")} pagados",
                                    Amount = totalCreditAmount
                                }, connection, transaction);
                            }
                            // else: Legacy SIN prepago: No hacer nada
                        }
                        else
                        {
                            // Cliente NUEVO: Crear DÉBITO inicial
                            var culture = new CultureInfo("es-AR");
                            string monthName = culture.DateTimeFormat.GetMonthName(startDate.Month);
                            string monthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName);
                            string concept = $"Alquiler {monthTitle} {startDate.Year}";
                            decimal debitAmount = dto.Amount;

                            // === NUEVA LÓGICA DE PRORRATEO ===
                            // Si entra el día 10 o después, se cobra proporcional.
                            if (startDate.Day >= 10)
                            {
                                int daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                                int daysToCharge = daysInMonth - startDate.Day;

                                decimal dailyRate = dto.Amount / daysInMonth;
                                debitAmount = dailyRate * daysToCharge;
                                
                                var roundedDebitAmount = RoundUpToNearest100(debitAmount);
                                debitAmount = roundedDebitAmount;

                                concept += $" (Proporcional {daysToCharge} días)";
                            }
                            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
                            {
                                RentalId = rentalId,
                                MovementDate = startDate,
                                MovementType = "DEBITO",
                                Concept = concept,
                                Amount = debitAmount, // <-- Usamos el monto calculado
                                PaymentId = null
                            }, connection, transaction);
                        }



                        // if (dto.LockerIds != null && dto.LockerIds.Count != 0) {
                        //     foreach (var lockerId in dto.LockerIds) {
                        //         if (!await lockerService.IsLockerAvailableAsync(lockerId, connection, transaction))
                        //             throw new InvalidOperationException($"Locker {lockerId} is already occupied.");
                        //     }
                        //     await lockerService.SetRentalTransactionAsync(rentalId, dto.LockerIds, connection, transaction);
                        // }

                        foreach (string email in dto.Emails)
                        {
                            if (!email.IsNullOrWhiteSpace())
                            {
                                Email emailEntity = new()
                                {
                                    ClientId = newClientId,
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
                                    ClientId = newClientId,
                                    Number = phone.Trim(),
                                    Type = "",
                                    Whatsapp = false
                                };
                                await phoneService.CreatePhoneTransaction(phoneEntity, connection, transaction);
                            }
                        }

                        Address address = new()
                        {
                            ClientId = newClientId,
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
                            RecordId = newClientId,
                        };

                        await activityLogService.CreateActivityLogTransactionAsync(activityLog, connection, transaction);

                        await transaction.CommitAsync();

                        return newClientId;
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
                Province = row["province"]?.ToString() ?? string.Empty, // El DTO de TS usa 'state', aquí 'Province'
                Cuit = row["cuit"]?.ToString() ?? string.Empty,
                Dni = row["dni"]?.ToString() ?? string.Empty,
                RegistrationDate = Convert.ToDateTime(row["registration_date"]),

                // Contact Information
                Address = row["street"]?.ToString() ?? string.Empty,
                // Email y Phone se cargan por separado más abajo

                // Payment & rental Information
                IvaCondition = row["iva_condition"]?.ToString() ?? string.Empty,
                PreferredPaymentMethod = row["preferred_payment_method"]?.ToString() ?? "No especificado",
                BillingTypeId = row["billing_type_id"] != DBNull.Value ? Convert.ToInt32(row["billing_type_id"]) : null,
                BillingType = row["billing_type"]?.ToString() ?? "No especificado",

                // --- CAMPOS ACTUALIZADOS ---
                IncreaseFrequencyMonths = Convert.ToInt32(row["increase_frequency_months"]),
                InitialAmount = row["initial_amount"] != DBNull.Value ? Convert.ToDecimal(row["initial_amount"]) : null,
                NextIncreaseDay = row["increase_anchor_date"] != DBNull.Value ? Convert.ToDateTime(row["increase_anchor_date"]) : DateTime.MinValue,
                // --- FIN CAMPOS ACTUALIZADOS ---

                ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
                OccupiedSpaces = row["occupied_spaces"] != DBNull.Value ? Convert.ToInt32(row["occupied_spaces"]) : 0,
                Balance = row["balance"] != DBNull.Value ? Convert.ToDecimal(row["balance"]) : 0,
                PaymentStatus = row["payment_status"]?.ToString() ?? "Desconocido",
                RentAmount = row["rent_amount"] != DBNull.Value ? Convert.ToDecimal(row["rent_amount"]) : 0m,

                // Other information
                Notes = row["notes"]?.ToString() ?? string.Empty,
            };

            // --- Carga Asíncrona de Lockers, Emails y Phones (sin cambios) ---
            List<GetLockerClientDetailDTO> lockers = await lockerService.GetLockersByClientIdAsync(id);
            clientDetail.LockersList = lockers;

            List<GetSpaceRequestDetailDto> spaceRequests = await _daoRentalSpaceRequest.GetRequestsByClientIdAsync(id);
            clientDetail.SpaceRequests = spaceRequests;

            var emailEntities = await emailService.GetEmailListByClientId(id);
            clientDetail.Email = emailEntities.Select(e => e.Address).ToArray();

            var phoneEntities = await phoneService.GetPhoneListByClientId(id); 
            clientDetail.Phone = phoneEntities.Select(p => p.Number).ToArray();

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
            ArgumentNullException.ThrowIfNull(dto);

            using (var connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var existingClient = await daoClient.GetClientByIdTransactionAsync(id, connection, transaction);
                        if (existingClient == null) return false;

                        // Verificar duplicados
                        if (!string.IsNullOrWhiteSpace(dto.Dni) && await daoClient.ExistsByDniAsync(dto.Dni, id, connection, transaction))
                            throw new InvalidOperationException("Ya existe otro cliente con este DNI.");
                        if (!string.IsNullOrWhiteSpace(dto.Cuit) && await daoClient.ExistsByCuitAsync(dto.Cuit, id, connection, transaction))
                            throw new InvalidOperationException("Ya existe otro cliente con este CUIT.");

                        // Mapear campos actualizables
                        Client clientToUpdate = new()
                        {
                            Id = id,
                            PaymentIdentifier = dto.PaymentIdentifier,
                            FirstName = dto.FirstName.Trim(),
                            LastName = dto.LastName.Trim(),
                            Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni.Trim(),
                            Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim(),
                            PreferredPaymentMethodId = dto.PreferredPaymentMethodId ?? existingClient.PreferredPaymentMethodId,
                            IvaCondition = string.IsNullOrWhiteSpace(dto.IvaCondition) ? existingClient.IvaCondition : dto.IvaCondition.Trim(),
                            BillingTypeId = dto.BillingTypeId ?? existingClient.BillingTypeId,
                            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? existingClient.Notes : dto.Notes.Trim(),

                            // Campos que NO se editan
                            RegistrationDate = dto.RegistrationDate, 
                            IncreaseFrequencyMonths = existingClient.IncreaseFrequencyMonths, 
                            InitialAmount = existingClient.InitialAmount,
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
                            // ... (Lógica para actualizar monto en rental_amount_history)
                            var lastAmountHistory = await rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(currentRental.Id, connection, transaction);
                            if (lastAmountHistory != null && dto.Amount != lastAmountHistory.Amount)
                            {
                                await rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(lastAmountHistory.Id, currentRental.Id, dto.Amount, DateTime.UtcNow.Date, connection, transaction);
                            }

                            if (dto.OccupiedSpaces != currentRental.OccupiedSpaces)
                            {
                                await rentalService.UpdateOccupiedSpacesTransactionAsync(currentRental.Id, dto.OccupiedSpaces, connection, transaction);
                            }

                            var currentLockerIds = await lockerService.GetLockerIdsByRentalIdTransactionAsync(currentRental.Id, connection, transaction);
                            var newLockerIds = dto.LockerIds ?? new List<int>();
                            var lockersToRemove = currentLockerIds.Except(newLockerIds).ToList();
                            var lockersToAdd = newLockerIds.Except(currentLockerIds).ToList();

                            if (lockersToRemove.Count != 0) {
                                await lockerService.UnassignLockersFromRentalTransactionAsync(lockersToRemove, connection, transaction);
                            }
                            if (lockersToAdd.Count != 0) {
                                foreach(var lockerIdToAdd in lockersToAdd) {
                                    if (!await lockerService.IsLockerAvailableAsync(lockerIdToAdd, connection, transaction)) {
                                        throw new InvalidOperationException($"El locker {lockerIdToAdd} ya no está disponible.");
                                    }
                                }
                                await lockerService.AssignLockersToRentalTransactionAsync(currentRental.Id, lockersToAdd, connection, transaction);
                            }

                            // Recalcular M3 si hubo cambios en lockers
                            if (lockersToAdd.Count != 0 || lockersToRemove.Count != 0) {
                                decimal newContractedM3 = await lockerService.CalculateTotalM3ForLockersAsync(newLockerIds, connection, transaction);
                                await rentalService.UpdateContractedM3TransactionAsync(currentRental.Id, newContractedM3, connection, transaction);
                            }
                        } else if (dto.LockerIds != null && dto.LockerIds.Count != 0) {
                            // Lógica para crear un nuevo rental si no existía y se asignan lockers?
                            Console.WriteLine($"Advertencia: Se asignaron lockers al cliente {id} pero no tiene un rental activo.");
                        }

                    // Lógica para Régimen de Aumento (si aplica)

                        ActivityLog activityLog = new()
                        { 
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

        public async Task<bool> DeactivateClientAsync(int clientId)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");

            // 1. Validación de Seguridad: Verificar lockers asignados
            var lockers = await lockerService.GetLockersByClientIdAsync(clientId);
            if (lockers != null && lockers.Count > 0)
            {
                throw new InvalidOperationException("No se puede dar de baja al cliente porque tiene lockers asignados. Libere los lockers primero.");
            }

            using (var connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var today = DateTime.UtcNow.Date;

                        // 2. Desactivar Cliente (Usando su propio DAO)
                        await daoClient.DeactivateClientTransactionAsync(clientId, connection, transaction);

                        // 3. Obtener Rental Activo usando el SERVICIO inyectado (Forma Correcta)
                        int? activeRentalId = await rentalService.GetActiveRentalIdByClientIdTransactionAsync(clientId, connection, transaction);

                        if (activeRentalId.HasValue)
                        {
                            // 4. Cerrar historial de montos usando el SERVICIO inyectado
                            await rentalAmountHistoryService.CloseOpenHistoriesByRentalIdTransactionAsync(activeRentalId.Value, today, connection, transaction);
                            
                            // 5. Finalizar Rental usando el SERVICIO inyectado
                            await rentalService.EndActiveRentalByClientIdTransactionAsync(clientId, today, connection, transaction);
                        }

                        await transaction.CommitAsync();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, $"Error deactivating client {clientId}");
                        throw;
                    }
                }
            }
        }

        private decimal RoundUpToNearest100(decimal amount)
        {
            if (amount == 0) return 0;
            return Math.Ceiling(amount / 100.0m) * 100;
        }

    }
}




    