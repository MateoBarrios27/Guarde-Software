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
using GuardeSoftwareAPI.Dtos.Phone;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Utils; // <- EL HELPER QUE ACABAMOS DE CREAR

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
        private readonly DaoClientMonthBalance _daoMonthBalance;
        private readonly DaoRentalAmountHistory _daoRentalAmountHistory;
        private readonly AccessDB accessDB;
        private readonly IClientMonthBalanceService _clientMonthBalanceService;

        public ClientService(AccessDB _accessDB, ILogger<ClientService> logger, IAccountMovementService _accountMovementService, IRentalService _rentalService, IRentalAmountHistoryService _rentalAmountHistoryService, ILockerService _lockerService, IActivityLogService _activityLogService, IEmailService _emailService, IPhoneService _phoneService, IAddressService _addressService, IClientMonthBalanceService clientMonthBalanceService)
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
            _daoRentalAmountHistory = new DaoRentalAmountHistory(_accessDB);
            accountMovementService = _accountMovementService;
            _logger = logger;
            _daoRentalSpaceRequest = new DaoRentalSpaceRequest(_accessDB);
            _daoMonthBalance = new DaoClientMonthBalance(_accessDB);
            _clientMonthBalanceService = clientMonthBalanceService;
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
                    FullName = row["full_name"]?.ToString() ?? string.Empty,
                    RegistrationDate = (DateTime)row["registration_date"],
                    Notes = row["notes"]?.ToString() ?? string.Empty,
                    Dni = row["dni"]?.ToString() ?? string.Empty,
                    Cuit = row["cuit"]?.ToString() ?? string.Empty,
                    PreferredPaymentMethodId = row["preferred_payment_method_id"] != DBNull.Value ? (int)row["preferred_payment_method_id"] : 0,
                    Balance = row["balance"] != DBNull.Value ? Convert.ToDecimal(row["balance"]) : 0m,
                    PreviousBalance = row.Table.Columns.Contains("PreviousBalance") && row["PreviousBalance"] != DBNull.Value ? Convert.ToDecimal(row["PreviousBalance"]) : 0m,
                    CurrentRent = row["rent_amount"] != DBNull.Value ? Convert.ToDecimal(row["rent_amount"]) : 0m,
                    IncreaseAnchorDate = row["IncreaseAnchorDate"] != DBNull.Value ? Convert.ToDateTime(row["IncreaseAnchorDate"]) : null,
                    PendingSurcharge = row["PendingSurcharge"] != DBNull.Value ? Convert.ToDecimal(row["PendingSurcharge"]) : 0m,
                    InterestAmount = row.Table.Columns.Contains("interest_amount") && row["interest_amount"] != DBNull.Value ? Convert.ToDecimal(row["interest_amount"]) : 0m,
                    LastGeneratedMonthYear = row["last_generated_month_year"]?.ToString() ?? string.Empty,
                    NextPaymentDay = row.Table.Columns.Contains("next_payment_day") && row["next_payment_day"] != DBNull.Value ? Convert.ToDateTime(row["next_payment_day"]) : null
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
                    FullName = row["full_name"]?.ToString() ?? string.Empty,
                    RegistrationDate = (DateTime)row["registration_date"],
                    Notes = row["notes"]?.ToString() ?? string.Empty,
                    Dni = row["dni"]?.ToString() ?? string.Empty,
                    Cuit = row["cuit"]?.ToString() ?? string.Empty,
                    PreferredPaymentMethodId = row["preferred_payment_method_id"] != DBNull.Value ? (int)row["preferred_payment_method_id"] : 0,
                    PreviousBalance = row.Table.Columns.Contains("PreviousBalance") && row["PreviousBalance"] != DBNull.Value ? Convert.ToDecimal(row["PreviousBalance"]) : 0m,
                };
                clients.Add(client);
            }
            return clients;
        }

        public async Task<int> CreateClientAsync(CreateClientDTO dto)
{
    ArgumentNullException.ThrowIfNull(dto);
    if (string.IsNullOrWhiteSpace(dto.FullName)) throw new ArgumentException("El nombre completo es requerido.");
    if (dto.Amount < 0) throw new ArgumentException("El monto debe ser mayor que 0.");
    if (dto.LockerIds.Any(id => id <= 0)) throw new ArgumentException("Los IDs de los casilleros deben ser números positivos.");
    if (dto.LockerIds.Distinct().Count() != dto.LockerIds.Count) throw new ArgumentException("No se permiten IDs de casilleros duplicados.");
    if (dto.UserID <= 0) throw new ArgumentException("El ID del usuario es inválido.");

    if (!string.IsNullOrEmpty(dto.Dni) && string.IsNullOrWhiteSpace(dto.Dni))
        throw new ArgumentException("El DNI no puede estar vacío o contener solo espacios en blanco.", nameof(dto.Dni));

    if (dto.IsLegacyClient)
    {
        if (dto.StartDate == default) throw new ArgumentException("La fecha de inicio de cliente heredado es requerida.");
        if (!dto.LegacyInitialAmount.HasValue || dto.LegacyInitialAmount < 0) throw new ArgumentException("El monto inicial de cliente heredado es requerido.");
        if (!dto.LegacyNextIncreaseDate.HasValue) throw new ArgumentException("La fecha de próxima incremento de cliente heredado es requerida.");
    }
    
    // FIX UTC: Usa la hora de Argentina
    DateTime argTime = TimeHelper.GetArgentinaTime();
    DateTime startDate = dto.IsLegacyClient ? dto.StartDate : argTime.Date;
    DateTime registrationDate = dto.IsLegacyClient ? dto.RegistrationDate : argTime.Date;
    var today = argTime.Date;
    decimal calculatedTotalM3 = 0;
    
    if (dto.SpaceRequests != null && dto.SpaceRequests.Count != 0)
    {
        calculatedTotalM3 = dto.SpaceRequests.Sum(r => r.M3 * r.Quantity);
    }
    else 
    {
        calculatedTotalM3 = dto.ContractedM3 ?? 0m;
    }

    using (var connection = accessDB.GetConnectionClose())
    {
        await connection.OpenAsync();
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                if (dto.PaymentIdentifier == null || dto.PaymentIdentifier.Value <= 0)
                {
                    decimal maxIdentifier = await daoClient.GetMaxPaymentIdentifierAsync(connection, transaction);
                    dto.PaymentIdentifier = maxIdentifier + 0.01m; 
                }

                if (dto.Dni != null && await daoClient.ExistsByDniAsync(dto.Dni, connection, transaction))
                {
                    throw new InvalidOperationException("Ya existe un cliente con este DNI.");
                }

                if (dto.Cuit != null && !dto.Cuit.IsNullOrEmpty() && await daoClient.ExistsByCuitAsync(dto.Cuit, connection, transaction))
                {
                    throw new InvalidOperationException("Ya existe un cliente con este CUIT.");
                }

                if (dto.PaymentIdentifier != null && await daoClient.ExistsByPaymentIdentifierAsync(dto.PaymentIdentifier.Value, connection, transaction))
                {
                    throw new InvalidOperationException("Ya existe un cliente con este Identificador de Pago.");
                }

                if (dto.FullName != null && await daoClient.ExistsByFullNameAsync(dto.FullName, connection, transaction))
                {
                    throw new InvalidOperationException("Ya existe un cliente con este nombre completo.");
                }

                Client client = new()
                {
                    PaymentIdentifier = dto.PaymentIdentifier,
                    FullName = dto.FullName.Trim(),
                    RegistrationDate = registrationDate,
                    Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni.Trim(),
                    Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim(),
                    PreferredPaymentMethodId = dto.PreferredPaymentMethodId,
                    IvaCondition = string.IsNullOrWhiteSpace(dto.IvaCondition) ? null : dto.IvaCondition.Trim(),
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    BillingTypeId = dto.BillingTypeId,
                    IncreaseFrequencyMonths = dto.IsLegacy6MonthPromo ? 6 : 4,
                    InitialAmount = dto.IsLegacyClient ? dto.LegacyInitialAmount : dto.Amount,
                    ReceiveCommunications = dto.ReceiveCommunications
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
                    DateTime calculationBaseDate = startDate;

                    if (startDate.Day > 20)
                    {
                        calculationBaseDate = startDate.AddMonths(1);
                    }

                    var firstAnniversary = calculationBaseDate.AddMonths(frequency - 1); 
                    nextIncreaseAnchorDate = new DateTime(firstAnniversary.Year, firstAnniversary.Month, 1);
                }

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
                            M3 = req.M3,
                            Comment = req.Comment
                        };
                        await _daoRentalSpaceRequest.CreateRequestTransactionAsync(spaceRequest, connection, transaction);
                    }
                }
                
                if (dto.LockerIds != null && dto.LockerIds.Count != 0)
                {
                    foreach (var lockerIdToAdd in dto.LockerIds)
                    {
                        if (!await lockerService.IsLockerAvailableAsync(lockerIdToAdd, connection, transaction))
                        {
                            throw new InvalidOperationException($"El locker con ID {lockerIdToAdd} no está disponible.");
                        }
                    }
                    
                    await lockerService.AssignLockersToRentalTransactionAsync(rentalId, dto.LockerIds, connection, transaction);
                    await daoClient.OpenLockerHistoryTransactionAsync(newClientId, dto.LockerIds, connection, transaction);
                }

                // 5. Crear Historial de Monto(s)
                if (dto.IsLegacyClient)
                {
                    await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(new RentalAmountHistory
                    {
                        RentalId = rentalId,
                        Amount = dto.LegacyInitialAmount ?? dto.Amount,
                        StartDate = startDate, 
                        EndDate = null 
                    }, connection, transaction);
                    
                    if (dto.Amount != (dto.LegacyInitialAmount ?? dto.Amount))
                    {
                        var lastAmountHistory = await rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(rentalId, connection, transaction);
                        if (lastAmountHistory != null)
                        {
                            await rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(lastAmountHistory.Id, rentalId, dto.Amount, argTime, connection, transaction);
                        }
                    }
                }
                else
                {
                    await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(new RentalAmountHistory
                    {
                        RentalId = rentalId,
                        Amount = dto.Amount,
                        StartDate = argTime,
                        EndDate = null
                    }, connection, transaction);
                }

                // ====================================================================================
                // 6. MOVIMIENTOS INICIALES Y CREACIÓN DEL ESTADO DE CUENTA MENSUAL
                // ====================================================================================
                
                var culture = new CultureInfo("es-AR");
                
                // MAGIA ACÁ: Si es legacy, el mes actual contable es HOY. Si es nuevo, es la startDate.
                string currentMonthStr = dto.IsLegacyClient ? today.ToString("MM/yyyy") : startDate.ToString("MM/yyyy");

                if (dto.IsLegacyClient)
                {
                    string monthName = culture.DateTimeFormat.GetMonthName(today.Month);
                    string monthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName);

                    await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
                    {
                        RentalId = rentalId,
                        MovementDate = today, // Débito con fecha de hoy
                        MovementType = "DEBITO",
                        Concept = $"Alquiler {monthTitle} {today.Year}", // Concepto del mes actual
                        Amount = dto.Amount,
                        PaymentId = null
                    }, connection, transaction);

                    decimal paidAmount = 0m;

                    if (dto.PrepaidMonths > 0 && dto.Amount > 0)
                    {
                        paidAmount = dto.PrepaidMonths * dto.Amount;
                        await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
                        {
                            RentalId = rentalId,
                            MovementDate = today, // Crédito con fecha de hoy
                            MovementType = "CREDITO",
                            Concept = $"Crédito inicial por {dto.PrepaidMonths} {(dto.PrepaidMonths == 1 ? "mes" : "meses")} pagados",
                            Amount = paidAmount
                        }, connection, transaction);
                    }

                    // A. Crear la fila mensual Legacy (1 SOLA FILA DEL MES ACTUAL)
                    await _daoMonthBalance.CreateMonthBalanceTransactionAsync(new ClientMonthBalance
                    {
                        RentalId = rentalId,
                        MonthYear = currentMonthStr,
                        PreviousBalance = 0m,        // Nace sin saldo anterior
                        Interests = 0m,
                        MonthlyDebits = dto.Amount,  // Abono actual
                        Paid = paidAmount,
                        AdvancedPayment = 0m
                    }, connection, transaction);
                }
                else
                {
                    if (startDate.Day < 10)
                    {
                        string monthName = culture.DateTimeFormat.GetMonthName(startDate.Month);
                        string monthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName);

                        await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
                        {
                            RentalId = rentalId,
                            MovementDate = startDate,
                            MovementType = "DEBITO",
                            Concept = $"Alquiler {monthTitle} {startDate.Year}",
                            Amount = dto.Amount,
                            PaymentId = null
                        }, connection, transaction);

                        // B1. Fila de mes actual Puro (1 SOLA FILA)
                        await _daoMonthBalance.CreateMonthBalanceTransactionAsync(new ClientMonthBalance
                        {
                            RentalId = rentalId,
                            MonthYear = currentMonthStr,
                            PreviousBalance = 0m,
                            Interests = 0m,
                            MonthlyDebits = dto.Amount,
                            Paid = 0m,
                            AdvancedPayment = 0m
                        }, connection, transaction);
                    }
                    else
                    {
                        // --- CASO DESPUÉS DEL DÍA 10 ---
                        int daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                        int daysToCharge = daysInMonth - startDate.Day; 
                        decimal dailyRate = dto.Amount / daysInMonth;
                        decimal proportionalRaw = dailyRate * daysToCharge;
                        decimal debitAmountProportional = RoundToNearest1000(proportionalRaw);

                        string currentMonthName = culture.DateTimeFormat.GetMonthName(startDate.Month);
                        string currentMonthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(currentMonthName);

                        // Movimiento Diario: Proporcional (Mes actual)
                        await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
                        {
                            RentalId = rentalId,
                            MovementDate = startDate,
                            MovementType = "DEBITO",
                            Concept = $"Alquiler {currentMonthTitle} {startDate.Year} (Proporcional {daysToCharge} días)",
                            Amount = debitAmountProportional,
                            PaymentId = null
                        }, connection, transaction);

                        // Movimiento Diario: Mes Completo (Mes siguiente)
                        DateTime nextMonthDate = startDate.AddMonths(1);
                        string nextMonthName = culture.DateTimeFormat.GetMonthName(nextMonthDate.Month);
                        string nextMonthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nextMonthName);

                        await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
                        {
                            RentalId = rentalId,
                            MovementDate = startDate, 
                            MovementType = "DEBITO",
                            Concept = $"Alquiler {nextMonthTitle} {nextMonthDate.Year}",
                            Amount = dto.Amount,
                            PaymentId = null
                        }, connection, transaction);

                        // B2. EL EXCEL (LA TABLA QUE VOS VES): 
                        // ¡UNA SOLA PUTA FILA! Creada para el mes siguiente. 
                        // Saldo Anterior = Proporcional. Abono = Cuota.
                        await _daoMonthBalance.CreateMonthBalanceTransactionAsync(new ClientMonthBalance
                        {
                            RentalId = rentalId,
                            MonthYear = nextMonthDate.ToString("MM/yyyy"), 
                            PreviousBalance = debitAmountProportional,     
                            Interests = 0m,
                            MonthlyDebits = dto.Amount,                    
                            Paid = 0m,
                            AdvancedPayment = 0m
                        }, connection, transaction);
                    }
                }

                await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rentalId, connection, transaction);

                // (Emails, Phones, etc...)
                foreach (string email in dto.Emails)
                {
                    if (!string.IsNullOrWhiteSpace(email))
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

                if (dto.Phones != null)
                {
                    foreach (var phone in dto.Phones)
                    {
                        if (!string.IsNullOrWhiteSpace(phone.Number))
                        {
                            Phone phoneEntity = new()
                            {
                                ClientId = newClientId,
                                Number = phone.Number.Trim(),
                                Type = "",
                                Whatsapp = phone.Whatsapp
                            };
                            await phoneService.CreatePhoneTransaction(phoneEntity, connection, transaction);
                        }
                    }
                }

                Address address = new()
                {
                    ClientId = newClientId,
                    Street = dto.AddressDto.Street?.Trim() ?? string.Empty,
                    City = "",   
                    Province = "" 
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
                FullName = row["full_name"]?.ToString() ?? string.Empty,
                
                Cuit = row["cuit"]?.ToString() ?? string.Empty,
                Dni = row["dni"]?.ToString() ?? string.Empty,
                RegistrationDate = Convert.ToDateTime(row["registration_date"]),

                // Contact Information
                Address = row["street"]?.ToString() ?? string.Empty,
                City = row["city"]?.ToString() ?? string.Empty,
                Province = row["province"]?.ToString() ?? string.Empty, 
                // Email y Phone se cargan por separado más abajo

                // Payment & rental Information
                IvaCondition = row["iva_condition"]?.ToString() ?? string.Empty,
                PreferredPaymentMethod = row["preferred_payment_method"]?.ToString() ?? "No especificado",
                BillingTypeId = row["billing_type_id"] != DBNull.Value ? Convert.ToInt32(row["billing_type_id"]) : null,
                BillingType = row["billing_type"]?.ToString() ?? "No especificado",
                TotalPaid = Convert.ToDecimal(row["total_paid"]),

                // --- CAMPOS ACTUALIZADOS ---
                IncreaseFrequencyMonths = Convert.ToInt32(row["increase_frequency_months"]),
                InitialAmount = row["initial_amount"] != DBNull.Value ? Convert.ToDecimal(row["initial_amount"]) : null,
                NextIncreaseDay = row["increase_anchor_date"] != DBNull.Value ? Convert.ToDateTime(row["increase_anchor_date"]) : DateTime.MinValue,
                // --- FIN CAMPOS ACTUALIZADOS ---

                ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
                OccupiedSpaces = row["occupied_spaces"] != DBNull.Value ? Convert.ToInt32(row["occupied_spaces"]) : 0,
                Balance = row["balance"] != DBNull.Value ? Convert.ToDecimal(row["balance"]) : 0,
                InterestAmount = row["interest_amount"] != DBNull.Value ? Convert.ToDecimal(row["interest_amount"]) : 0m,
                PaymentStatus = row["payment_status"]?.ToString() ?? "Desconocido",
                RentAmount = row["rent_amount"] != DBNull.Value ? Convert.ToDecimal(row["rent_amount"]) : 0m,

                // Other information
                Notes = row["notes"]?.ToString() ?? string.Empty,
                NextPaymentDay = row["next_payment_day"] != DBNull.Value ? Convert.ToDateTime(row["next_payment_day"]) : DateTime.MinValue,
                ReceiveCommunications = Convert.ToBoolean(row["receive_communications"]),
                Color = row["color"] != DBNull.Value ? row["color"].ToString() : null,
                Comment = row["comment"] != DBNull.Value ? row["comment"].ToString() : null,
                CommentUpdatedAt = row["comment_updated_at"] != DBNull.Value ? Convert.ToDateTime(row["comment_updated_at"]) : null
            };

            // Contact Information
                // Armamos la dirección completa concatenando si existen datos viejos
                string street = row["street"]?.ToString() ?? string.Empty;
                string city = row["city"]?.ToString() ?? string.Empty;
                string province = row["province"]?.ToString() ?? string.Empty;

                string fullAddress = street;
                if (!string.IsNullOrWhiteSpace(city)) fullAddress += $", {city}";
                if (!string.IsNullOrWhiteSpace(province)) fullAddress += $", {province}";

                clientDetail.Address = fullAddress.TrimEnd(',', ' ');

            // --- Carga Asíncrona de Lockers, Emails y Phones (sin cambios) ---
            List<GetLockerClientDetailDTO> lockers = await lockerService.GetLockersByClientIdAsync(id);
            clientDetail.LockersList = lockers;

            List<GetSpaceRequestDetailDto> spaceRequests = await _daoRentalSpaceRequest.GetRequestsByClientIdAsync(id);
            clientDetail.SpaceRequests = spaceRequests;

            var emailEntities = await emailService.GetEmailListByClientId(id);
            clientDetail.Email = emailEntities.Select(e => e.Address).ToArray();

            var phoneEntities = await phoneService.GetPhoneListByClientId(id);

            clientDetail.Phones = phoneEntities
                .Select(p => new PhoneInputDto
                {
                    Number = p.Number,
                    Whatsapp = p.Whatsapp
                })
                .ToList();


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

                        if (!string.IsNullOrWhiteSpace(dto.Dni) && await daoClient.ExistsByDniAsync(dto.Dni, id, connection, transaction))
                            throw new InvalidOperationException("Ya existe otro cliente con este DNI.");
                        if (!string.IsNullOrWhiteSpace(dto.Cuit) && await daoClient.ExistsByCuitAsync(dto.Cuit, id, connection, transaction))
                            throw new InvalidOperationException("Ya existe otro cliente con este CUIT.");
                        if (dto.PaymentIdentifier != null && await daoClient.ExistsByPaymentIdentifierAsync(dto.PaymentIdentifier.Value, id, connection, transaction))
                            throw new InvalidOperationException("Ya existe otro cliente con este número identificador de pago.");
                        if (!string.IsNullOrWhiteSpace(dto.FullName) && await daoClient.ExistsByFullNameAsync(dto.FullName, id, connection, transaction))
                            throw new InvalidOperationException("Ya existe otro cliente con este nombre completo.");

                        Client clientToUpdate = new()
                        {
                            Id = id,
                            PaymentIdentifier = dto.PaymentIdentifier,
                            FullName = dto.FullName.Trim(),
                            Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni.Trim(),
                            Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim(),
                            PreferredPaymentMethodId = dto.PreferredPaymentMethodId ?? existingClient.PreferredPaymentMethodId,
                            IvaCondition = string.IsNullOrWhiteSpace(dto.IvaCondition) ? existingClient.IvaCondition : dto.IvaCondition.Trim(),
                            BillingTypeId = dto.BillingTypeId ?? existingClient.BillingTypeId,
                            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? existingClient.Notes : dto.Notes.Trim(),

                            // Campos que NO se editan
                            RegistrationDate = dto.RegistrationDate, 
                            IncreaseFrequencyMonths = existingClient.IncreaseFrequencyMonths, 
                            InitialAmount = dto.LegacyInitialAmount,
                            ReceiveCommunications = dto.ReceiveCommunications
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
                        if (dto.Phones != null)
                        {
                            foreach (var phone in dto.Phones)
                            {
                                if (!string.IsNullOrWhiteSpace(phone.Number))
                                {
                                    await phoneService.CreatePhoneTransaction(
                                        new Phone
                                        {
                                            ClientId = id,
                                            Number = phone.Number.Trim(),
                                            Type = "",
                                            Whatsapp = phone.Whatsapp
                                        },
                                        connection,
                                        transaction
                                    );
                                }
                            }
                        }

                        await addressService.DeleteAddressByClientIdTransactionAsync(id, connection, transaction);

                        if (dto.AddressDto != null && !string.IsNullOrWhiteSpace(dto.AddressDto.Street)) {
                            await addressService.CreateAddressTransaction(new Address { 
                                ClientId = id, 
                                Street = dto.AddressDto.Street.Trim(), 
                                City = "", 
                                Province = "" 
                            }, connection, transaction);
                        }

                        var currentRental = await rentalService.GetRentalByClientIdTransactionAsync(id, connection, transaction);

                        if (currentRental != null)
                        {
                            var lastAmountHistory = await rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(currentRental.Id, connection, transaction);
                            
                            if (lastAmountHistory != null && dto.Amount != lastAmountHistory.Amount)
                            {
                                // FIX UTC: Usa la hora de Argentina
                                DateTime argTime = TimeHelper.GetArgentinaTime();
                                DateTime nextPaymentDate = new DateTime(argTime.Year, argTime.Month, 1);
                                
                                string nextPaymentQuery = @"
                                    SELECT TOP 1 
                                        month_year, 
                                        (balance - paid - advanced_payment) as NetBalance 
                                    FROM client_month_balances 
                                    WHERE rental_id = @rentalId 
                                    ORDER BY id DESC";

                                using (var cmdNext = new SqlCommand(nextPaymentQuery, connection, transaction))
                                {
                                    cmdNext.Parameters.AddWithValue("@rentalId", currentRental.Id);
                                    using (var reader = await cmdNext.ExecuteReaderAsync())
                                    {
                                        if (await reader.ReadAsync())
                                        {
                                            string my = reader["month_year"].ToString();
                                            decimal netBalance = Convert.ToDecimal(reader["NetBalance"]);
                                            
                                            if (!string.IsNullOrEmpty(my) && my.Length == 7)
                                            {
                                                int m = int.Parse(my.Substring(0, 2));
                                                int y = int.Parse(my.Substring(3, 4));
                                                DateTime lastGeneratedMonth = new DateTime(y, m, 1);
                                                
                                                if (netBalance > 0)
                                                {
                                                    nextPaymentDate = lastGeneratedMonth;
                                                }
                                                else
                                                {
                                                    nextPaymentDate = lastGeneratedMonth.AddMonths(1);
                                                }
                                            }
                                        }
                                    }
                                }

                                if (lastAmountHistory.StartDate > nextPaymentDate)
                                {
                                    nextPaymentDate = lastAmountHistory.StartDate;
                                }

                                await _daoRentalAmountHistory.EndRentalAmountHistoryTransactionAsync(lastAmountHistory.Id, nextPaymentDate.AddDays(-1), connection, transaction);

                                await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(new RentalAmountHistory 
                                {
                                    RentalId = currentRental.Id,
                                    Amount = dto.Amount,
                                    StartDate = nextPaymentDate,
                                    EndDate = null
                                }, connection, transaction);

                                string updateFutureDebitsQuery = @"
                                    UPDATE am
                                    SET am.amount = @newAmount
                                    FROM account_movements am
                                    LEFT JOIN client_month_balances cmb ON am.rental_id = cmb.rental_id 
                                        AND FORMAT(am.movement_date, 'MM/yyyy') = cmb.month_year
                                    WHERE am.rental_id = @rentalId 
                                      AND am.movement_type = 'DEBITO'
                                      AND am.concept LIKE 'Alquiler %'
                                      AND am.movement_date >= @nextPaymentDate
                                      AND (cmb.id IS NULL OR (cmb.balance - cmb.paid - cmb.advanced_payment) > 0)";

                                using var cmdUpdateDebits = new SqlCommand(updateFutureDebitsQuery, connection, transaction);
                                cmdUpdateDebits.Parameters.AddWithValue("@newAmount", dto.Amount);
                                cmdUpdateDebits.Parameters.AddWithValue("@rentalId", currentRental.Id);
                                cmdUpdateDebits.Parameters.AddWithValue("@nextPaymentDate", nextPaymentDate);
                                
                                await cmdUpdateDebits.ExecuteNonQueryAsync();
                            }

                            if (dto.OccupiedSpaces != currentRental.OccupiedSpaces)
                            {
                                await rentalService.UpdateOccupiedSpacesTransactionAsync(currentRental.Id, dto.OccupiedSpaces, connection, transaction);
                            }

                            if (dto.LegacyNextIncreaseDate.HasValue) 
                            {
                                if (currentRental.IncreaseAnchorDate != dto.LegacyNextIncreaseDate.Value)
                                {
                                    await rentalService.UpdateIncreaseAnchorDateTransactionAsync(currentRental.Id, dto.LegacyNextIncreaseDate.Value, connection, transaction);
                                }
                            }

                            var currentLockerIds = await lockerService.GetLockerIdsByRentalIdTransactionAsync(currentRental.Id, connection, transaction);
                            var newLockerIds = dto.LockerIds ?? [];
                            var lockersToRemove = currentLockerIds.Except(newLockerIds).ToList();
                            var lockersToAdd = newLockerIds.Except(currentLockerIds).ToList();

                            if (lockersToRemove.Count != 0) {
                                await lockerService.UnassignLockersFromRentalTransactionAsync(lockersToRemove, connection, transaction);
                                await daoClient.CloseLockerHistoryTransactionAsync(id, lockersToRemove, connection, transaction);
                            }
                            if (lockersToAdd.Count != 0) {
                                foreach(var lockerIdToAdd in lockersToAdd) {
                                    if (!await lockerService.IsLockerAvailableAsync(lockerIdToAdd, connection, transaction)) {
                                        throw new InvalidOperationException($"El locker {lockerIdToAdd} ya no está disponible.");
                                    }
                                }
                                await lockerService.AssignLockersToRentalTransactionAsync(currentRental.Id, lockersToAdd, connection, transaction);
                                await daoClient.OpenLockerHistoryTransactionAsync(id, lockersToAdd, connection, transaction);
                            }

                            if (lockersToAdd.Count != 0 || lockersToRemove.Count != 0) {
                                decimal newContractedM3 = await lockerService.CalculateTotalM3ForLockersAsync(newLockerIds, connection, transaction);
                                await rentalService.UpdateContractedM3TransactionAsync(currentRental.Id, newContractedM3, connection, transaction);
                            }

                            await _clientMonthBalanceService.RebuildForRentalTransactionAsync(currentRental.Id, connection, transaction);
                            
                        } else if (dto.LockerIds != null && dto.LockerIds.Count != 0) {
                            Console.WriteLine($"Advertencia: Se asignaron lockers al cliente {id} pero no tiene un rental activo.");
                        }


                        ActivityLog activityLog = new()
                        { 
                            UserId = dto.UserID,
                            LogDate = TimeHelper.GetArgentinaTime(), // FIX UTC
                            Action = "UPDATE",
                            TableName = "clients",
                            RecordId = id,
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

            using (var connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var today = TimeHelper.GetArgentinaTime().Date; // FIX UTC
                        
                        int? activeRentalId = await rentalService.GetActiveRentalIdByClientIdTransactionAsync(clientId, connection, transaction);

                        if (activeRentalId.HasValue)
                        {
                            var lockerIds = await lockerService.GetLockerIdsByRentalIdTransactionAsync(activeRentalId.Value, connection, transaction);
                            if (lockerIds != null && lockerIds.Count > 0)
                            {
                                await lockerService.UnassignLockersFromRentalTransactionAsync(lockerIds, connection, transaction);
                                await daoClient.CloseLockerHistoryTransactionAsync(clientId, lockerIds, connection, transaction);
                                await rentalService.UpdateContractedM3TransactionAsync(activeRentalId.Value, 0m, connection, transaction);
                            }

                            await rentalAmountHistoryService.CloseOpenHistoriesByRentalIdTransactionAsync(activeRentalId.Value, today, connection, transaction);
                            
                            await rentalService.EndActiveRentalByClientIdTransactionAsync(clientId, today, connection, transaction);
                        }

                        await daoClient.DeactivateClientTransactionAsync(clientId, connection, transaction);

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

        private decimal RoundToNearest1000(decimal amount)
        {
            if (amount == 0) return 0;
            return Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
        }

        public async Task ReactivateClientAsync(int clientId, CreateClientDTO dto)
        {
            if (clientId <= 0) throw new ArgumentException("ID de cliente inválido.");
            ArgumentNullException.ThrowIfNull(dto);

            if (dto.UserID <= 0) throw new ArgumentException("El ID del usuario es inválido (El frontend no está enviando el UserID en el DTO).");

            using (var connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var existingClient = await daoClient.GetClientByIdTransactionAsync(clientId, connection, transaction);
                        if (existingClient == null) throw new Exception("Cliente no encontrado.");

                        decimal maxIdentifier = await daoClient.GetMaxPaymentIdentifierAsync(connection, transaction);
                        decimal newPaymentIdentifier = maxIdentifier + 0.01m;
                        await daoClient.ReactivateClientTransactionAsync(clientId, newPaymentIdentifier, connection, transaction);

                        Client clientToUpdate = new()
                        {
                            Id = clientId,
                            PaymentIdentifier = newPaymentIdentifier, 
                            FullName = dto.FullName.Trim(),
                            Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni.Trim(),
                            Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim(),
                            PreferredPaymentMethodId = dto.PreferredPaymentMethodId ?? existingClient.PreferredPaymentMethodId,
                            IvaCondition = string.IsNullOrWhiteSpace(dto.IvaCondition) ? existingClient.IvaCondition : dto.IvaCondition.Trim(),
                            BillingTypeId = dto.BillingTypeId ?? existingClient.BillingTypeId,
                            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? existingClient.Notes : dto.Notes.Trim(),
                            RegistrationDate = existingClient.RegistrationDate, 
                            IncreaseFrequencyMonths = existingClient.IncreaseFrequencyMonths, 
                            InitialAmount = existingClient.InitialAmount,
                            ReceiveCommunications = dto.ReceiveCommunications
                        };
                        await daoClient.UpdateClientTransactionAsync(clientToUpdate, connection, transaction);

                        await emailService.DeleteEmailsByClientIdTransactionAsync(clientId, connection, transaction);
                        if (dto.Emails != null) {
                            foreach (string emailAddr in dto.Emails.Where(e => !string.IsNullOrWhiteSpace(e))) {
                                await emailService.CreateEmailTransaction(new Email { ClientId = clientId, Address = emailAddr.Trim(), Type = "" }, connection, transaction);
                            }
                        }

                        await phoneService.DeletePhonesByClientIdTransactionAsync(clientId, connection, transaction);
                        if (dto.Phones != null) {
                            foreach (var phone in dto.Phones.Where(p => !string.IsNullOrWhiteSpace(p.Number))) {
                                await phoneService.CreatePhoneTransaction(new Phone { ClientId = clientId, Number = phone.Number.Trim(), Type = "", Whatsapp = phone.Whatsapp }, connection, transaction);
                            }
                        }

                        await addressService.DeleteAddressByClientIdTransactionAsync(clientId, connection, transaction);
                        if (dto.AddressDto != null && !string.IsNullOrWhiteSpace(dto.AddressDto.Street)) {
                            await addressService.CreateAddressTransaction(new Address { ClientId = clientId, Street = dto.AddressDto.Street.Trim(), City = "", Province = "" }, connection, transaction);
                        }

                        decimal calculatedTotalM3 = (dto.SpaceRequests != null && dto.SpaceRequests.Count != 0) 
                            ? dto.SpaceRequests.Sum(r => r.M3 * r.Quantity) : (dto.ContractedM3 ?? 0m);

                        // FIX UTC
                        DateTime startDate = TimeHelper.GetArgentinaTime();
                        DateTime calculationBaseDate = startDate.Date;
                        if (calculationBaseDate.Day > 20) calculationBaseDate = calculationBaseDate.AddMonths(1);
                        int frequency = existingClient.IncreaseFrequencyMonths > 0 ? existingClient.IncreaseFrequencyMonths : 4;
                        var firstAnniversary = calculationBaseDate.AddMonths(frequency - 1); 
                        DateTime nextIncreaseAnchorDate = new DateTime(firstAnniversary.Year, firstAnniversary.Month, 1);

                        Rental rental = new()
                        {
                            ClientId = clientId,
                            StartDate = startDate,
                            ContractedM3 = calculatedTotalM3,
                            MonthsUnpaid = 0,
                            PriceLockEndDate = null,
                            IncreaseAnchorDate = nextIncreaseAnchorDate,
                            OccupiedSpaces = dto.OccupiedSpaces,
                        };
                        int rentalId = await rentalService.CreateRentalTransactionAsync(rental, connection, transaction);

                        if (dto.SpaceRequests != null && dto.SpaceRequests.Count != 0)
                        {
                            foreach (var req in dto.SpaceRequests)
                            {
                                await _daoRentalSpaceRequest.CreateRequestTransactionAsync(new RentalSpaceRequest { RentalId = rentalId, WarehouseId = req.WarehouseId, Quantity = req.Quantity, M3 = req.M3, Comment = req.Comment }, connection, transaction);
                            }
                        }
                        else if (dto.LockerIds != null && dto.LockerIds.Count != 0)
                        {
                            foreach (var lockerIdToAdd in dto.LockerIds)
                            {
                                if (!await lockerService.IsLockerAvailableAsync(lockerIdToAdd, connection, transaction))
                                    throw new InvalidOperationException($"El locker {lockerIdToAdd} ya no está disponible.");
                            }
                            await lockerService.AssignLockersToRentalTransactionAsync(rentalId, dto.LockerIds, connection, transaction);
                            await daoClient.OpenLockerHistoryTransactionAsync(clientId, dto.LockerIds, connection, transaction);
                        }

                        await rentalAmountHistoryService.CreateRentalAmountHistoryTransactionAsync(new RentalAmountHistory
                        {
                            RentalId = rentalId, Amount = dto.Amount, StartDate = startDate, EndDate = null
                        }, connection, transaction);

                        var culture = new CultureInfo("es-AR");
                        if (startDate.Day < 10)
                        {
                            string monthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(startDate.Month));
                            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement { RentalId = rentalId, MovementDate = startDate, MovementType = "DEBITO", Concept = $"Alquiler {monthTitle} {startDate.Year} (Reactivación)", Amount = dto.Amount, PaymentId = null }, connection, transaction);
                            await _daoMonthBalance.CreateMonthBalanceTransactionAsync(new ClientMonthBalance
                            {
                                RentalId = rentalId,
                                MonthYear = startDate.ToString("MM/yyyy"),
                                PreviousBalance = 0m,
                                Interests = 0m,
                                MonthlyDebits = dto.Amount,
                                Paid = 0m,
                                AdvancedPayment = 0m
                            }, connection, transaction);
                        }
                        else
                        {
                            int daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                            int daysToCharge = daysInMonth - startDate.Day; 
                            decimal dailyRate = dto.Amount / daysInMonth;
                            decimal debitAmountProportional = RoundToNearest1000(dailyRate * daysToCharge);

                            string currentMonthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(startDate.Month));
                            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement { RentalId = rentalId, MovementDate = startDate, MovementType = "DEBITO", Concept = $"Alquiler {currentMonthTitle} {startDate.Year} (Reactivación Proporcional {daysToCharge} días)", Amount = debitAmountProportional, PaymentId = null }, connection, transaction);

                            DateTime nextMonthDate = startDate.AddMonths(1);
                            string nextMonthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(nextMonthDate.Month));
                            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement { RentalId = rentalId, MovementDate = startDate, MovementType = "DEBITO", Concept = $"Alquiler {nextMonthTitle} {nextMonthDate.Year}", Amount = dto.Amount, PaymentId = null }, connection, transaction);
                            await _daoMonthBalance.CreateMonthBalanceTransactionAsync(new ClientMonthBalance
                            {
                                RentalId = rentalId,
                                MonthYear = nextMonthDate.ToString("MM/yyyy"),
                                PreviousBalance = debitAmountProportional,
                                Interests = 0m,
                                MonthlyDebits = dto.Amount,
                                Paid = 0m,
                                AdvancedPayment = 0m
                            }, connection, transaction);
                        }

                        await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rentalId, connection, transaction);

                        ActivityLog activityLog = new() { UserId = dto.UserID, LogDate = TimeHelper.GetArgentinaTime(), Action = "REACTIVATE", TableName = "clients", RecordId = clientId };
                        await activityLogService.CreateActivityLogTransactionAsync(activityLog, connection, transaction);

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }                  
                }
            }
        }

        public async Task<List<ClientLockerHistory>> GetClientLockerHistoryAsync(int clientId)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");

            return await daoClient.GetClientLockerHistoryAsync(clientId);
        }

        public async Task<bool> UpdateClientColorAsync(int clientId, string? color)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            return await daoClient.UpdateClientColorAsync(clientId, color);
        }

        public async Task<bool> UpdateClientCommentAsync(int clientId, string? comment)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            return await daoClient.UpdateClientCommentAsync(clientId, comment);
        }
    }
}