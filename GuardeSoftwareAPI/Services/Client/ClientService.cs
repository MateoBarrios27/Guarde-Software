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
        private readonly DaoMonthlyIncrease _daoMonthlyIncrease;

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
            _daoMonthlyIncrease = new DaoMonthlyIncrease(_accessDB);
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
                    DepartureStatus = row.Table.Columns.Contains("departure_status") && row["departure_status"] != DBNull.Value ? row["departure_status"].ToString() : null,
                    Balance = row["balance"] != DBNull.Value ? Convert.ToDecimal(row["balance"]) : 0m,
                    PreviousBalance = row.Table.Columns.Contains("PreviousBalance") && row["PreviousBalance"] != DBNull.Value ? Convert.ToDecimal(row["PreviousBalance"]) : 0m,
                    CurrentRent = row["rent_amount"] != DBNull.Value ? Convert.ToDecimal(row["rent_amount"]) : 0m,
                    IncreaseAnchorDate = row["IncreaseAnchorDate"] != DBNull.Value ? Convert.ToDateTime(row["IncreaseAnchorDate"]) : null,
                    PendingSurcharge = row["PendingSurcharge"] != DBNull.Value ? Convert.ToDecimal(row["PendingSurcharge"]) : 0m,
                    IsSixMonthPromotion = row.Table.Columns.Contains("is_six_month_promotion") && row["is_six_month_promotion"] != DBNull.Value && Convert.ToBoolean(row["is_six_month_promotion"]),
                    InterestAmount = row.Table.Columns.Contains("interest_amount") && row["interest_amount"] != DBNull.Value ? Convert.ToDecimal(row["interest_amount"]) : 0m,
                    LastGeneratedMonthYear = row["last_generated_month_year"]?.ToString() ?? string.Empty,
                    NextPaymentDay = row.Table.Columns.Contains("next_payment_day") && row["next_payment_day"] != DBNull.Value ? Convert.ToDateTime(row["next_payment_day"]) : null,
                    PlannedPaymentAmount = row.Table.Columns.Contains("planned_payment_amount") && row["planned_payment_amount"] != DBNull.Value ? Convert.ToDecimal(row["planned_payment_amount"]) : 0m,
                    HasPlannedPayment = row.Table.Columns.Contains("has_planned_payment") && row["has_planned_payment"] != DBNull.Value && Convert.ToBoolean(row["has_planned_payment"])
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
                    DepartureStatus = row.Table.Columns.Contains("departure_status") && row["departure_status"] != DBNull.Value ? row["departure_status"].ToString() : null,
                    IsSixMonthPromotion = row.Table.Columns.Contains("is_six_month_promotion") && row["is_six_month_promotion"] != DBNull.Value && Convert.ToBoolean(row["is_six_month_promotion"]),
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
    bool shouldGenerateProportional = !dto.IsLegacyClient && startDate.Day >= 10;
    int daysInStartMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);

    if (dto.UseManualProportional)
    {
        if (!shouldGenerateProportional)
            throw new ArgumentException("El proporcional manual sólo puede usarse cuando corresponde generar un proporcional de alta.");
        if (!dto.ProportionalDays.HasValue || dto.ProportionalDays.Value < 0 || dto.ProportionalDays.Value > daysInStartMonth)
            throw new ArgumentException($"La cantidad de días del proporcional debe estar entre 0 y {daysInStartMonth}.");
        if (!dto.ProportionalAmount.HasValue || dto.ProportionalAmount.Value < 0)
            throw new ArgumentException("El monto proporcional manual debe ser mayor o igual que 0.");
    }

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
                    IncreaseFrequencyMonths = dto.IsLegacy6MonthPromo || dto.IsSixMonthPromotion ? 6 : 4,
                    IsSixMonthPromotion = dto.IsLegacy6MonthPromo || dto.IsSixMonthPromotion,
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
                        int daysToCharge = dto.UseManualProportional
                            ? dto.ProportionalDays!.Value
                            : daysInStartMonth - startDate.Day;
                        decimal debitAmountProportional = dto.UseManualProportional
                            ? dto.ProportionalAmount!.Value
                            : RoundToNearest1000(dto.Amount / daysInStartMonth * daysToCharge);

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
                    LogDate = TimeHelper.GetArgentinaTime(),
                    Action = "CREATE",
                    TableName = "clients",
                    RecordId = newClientId,
                };

                await activityLogService.CreateActivityLogTransactionAsync(activityLog, connection, transaction);

                await rentalAmountHistoryService.NormalizeRentalAmountHistoryTransactionAsync(rentalId, connection, transaction);
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

        public async Task<List<RentalAmountHistoryItemDto>> GetClientRentalAmountHistoryAsync(int clientId)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");

            // Get active rental_id for the client
            string rentalQuery = @"SELECT TOP 1 rental_id FROM rentals WHERE client_id = @clientId AND active = 1 ORDER BY rental_id DESC";
            var rentalParam = new[] { new Microsoft.Data.SqlClient.SqlParameter("@clientId", clientId) };
            var rentalTable = await accessDB.GetTableAsync("rentals", rentalQuery, rentalParam);
            if (rentalTable.Rows.Count == 0) return [];
            int rentalId = Convert.ToInt32(rentalTable.Rows[0]["rental_id"]);

            // Get all history items ordered by start_date DESC
            string histQuery = @"
                SELECT rental_amount_history_id, amount, start_date, end_date
                FROM rental_amount_history
                WHERE rental_id = @rentalId
                ORDER BY start_date DESC, rental_amount_history_id DESC";
            var histParam = new[] { new Microsoft.Data.SqlClient.SqlParameter("@rentalId", rentalId) };
            var histTable = await accessDB.GetTableAsync("rental_amount_history", histQuery, histParam);

            var now = DateTime.UtcNow.AddHours(-3); // Argentina time
            var result = new List<RentalAmountHistoryItemDto>();
            foreach (System.Data.DataRow row in histTable.Rows)
            {
                var startDate = Convert.ToDateTime(row["start_date"]);
                var endDate = row["end_date"] != DBNull.Value ? Convert.ToDateTime(row["end_date"]) : (DateTime?)null;

                string status;
                if (startDate > now)
                    status = "planned";
                else if (!endDate.HasValue || endDate.Value >= now)
                    status = "active";
                else
                    status = "past";

                result.Add(new RentalAmountHistoryItemDto
                {
                    Id = Convert.ToInt32(row["rental_amount_history_id"]),
                    Amount = Convert.ToDecimal(row["amount"]),
                    StartDate = startDate,
                    EndDate = endDate,
                    Status = status
                });
            }
            return result;
        }

        public async Task AddClientRentalAmountEntryAsync(int clientId, decimal amount, int year, int month)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            if (amount < 0) throw new ArgumentException("El monto debe ser 0 o mayor.");

            // Get active rental_id
            string rentalQuery = @"SELECT TOP 1 rental_id FROM rentals WHERE client_id = @clientId AND active = 1 ORDER BY rental_id DESC";
            var rentalParam = new[] { new Microsoft.Data.SqlClient.SqlParameter("@clientId", clientId) };
            var rentalTable = await accessDB.GetTableAsync("rentals", rentalQuery, rentalParam);
            if (rentalTable.Rows.Count == 0) throw new InvalidOperationException("El cliente no tiene un alquiler activo.");
            int rentalId = Convert.ToInt32(rentalTable.Rows[0]["rental_id"]);

            var newStartDate = new DateTime(year, month, 1);
            int newHistoryId = 0;

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                // Close any overlapping open history entries
                var closeEndDate = newStartDate.AddSeconds(-1);
                string closeQuery = @"
                    UPDATE rental_amount_history
                    SET end_date = @EndDate
                    WHERE rental_id = @RentalId
                      AND end_date IS NULL
                      AND start_date < @NewStart";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(closeQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@EndDate", closeEndDate);
                    cmd.Parameters.AddWithValue("@RentalId", rentalId);
                    cmd.Parameters.AddWithValue("@NewStart", newStartDate);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert new entry
                string insertQuery = @"
                    INSERT INTO rental_amount_history (rental_id, amount, start_date)
                    OUTPUT INSERTED.rental_amount_history_id
                    VALUES (@RentalId, @Amount, @StartDate)";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(insertQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@RentalId", rentalId);
                    cmd.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Amount", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = amount });
                    cmd.Parameters.AddWithValue("@StartDate", newStartDate);
                    newHistoryId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                await rentalAmountHistoryService.NormalizeRentalAmountHistoryTransactionAsync(rentalId, connection, transaction);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // Rebuild balances
            await _clientMonthBalanceService.RebuildForRentalAsync(rentalId);

            await activityLogService.TryCreateActivityLogAsync(new ActivityLog
            {
                Action = "CREATE",
                TableName = "rental_amount_history",
                RecordId = newHistoryId,
                NewValue = JsonSerializer.Serialize(new { Id = newHistoryId, rentalId, amount, StartDate = newStartDate })
            });
        }

        public async Task UpdateClientRentalAmountEntryAsync(int clientId, int histId, decimal amount, int year, int month)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            if (histId <= 0) throw new ArgumentException("Invalid history ID.");
            if (amount < 0) throw new ArgumentException("El monto debe ser 0 o mayor.");

            // Verify the history belongs to this client's rental
            string verifyQuery = @"
                SELECT rah.rental_id
                FROM rental_amount_history rah
                JOIN rentals r ON rah.rental_id = r.rental_id
                WHERE rah.rental_amount_history_id = @HistId
                  AND r.client_id = @ClientId";
            var verifyParams = new[]
            {
                new Microsoft.Data.SqlClient.SqlParameter("@HistId", histId),
                new Microsoft.Data.SqlClient.SqlParameter("@ClientId", clientId)
            };
            var verifyTable = await accessDB.GetTableAsync("verify", verifyQuery, verifyParams);
            if (verifyTable.Rows.Count == 0) throw new InvalidOperationException("Tramo no encontrado o no pertenece al cliente.");
            int rentalId = Convert.ToInt32(verifyTable.Rows[0]["rental_id"]);
            RentalAmountHistoryItemDto? previousHistory = (await GetClientRentalAmountHistoryAsync(clientId))
                .FirstOrDefault(history => history.Id == histId);

            var newStartDate = new DateTime(year, month, 1);

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                // Update the entry
                string updateQuery = @"
                    UPDATE rental_amount_history
                    SET amount = @Amount, start_date = @StartDate
                    WHERE rental_amount_history_id = @HistId";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(updateQuery, connection, transaction))
                {
                    cmd.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Amount", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = amount });
                    cmd.Parameters.AddWithValue("@StartDate", newStartDate);
                    cmd.Parameters.AddWithValue("@HistId", histId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Si el tramo se movió a una fecha ya existente, el registro
                // editado es la versión válida y los duplicados se descartan.
                const string deleteDuplicatesQuery = @"
                    DELETE FROM rental_amount_history
                    WHERE rental_id = @RentalId
                      AND CAST(start_date AS date) = @StartDate
                      AND rental_amount_history_id <> @HistId";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(deleteDuplicatesQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@RentalId", rentalId);
                    cmd.Parameters.AddWithValue("@StartDate", newStartDate);
                    cmd.Parameters.AddWithValue("@HistId", histId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Recalculate end_dates for all entries of this rental (sort and fix chain)
                string fixQuery = @"
                    WITH Ordered AS (
                        SELECT rental_amount_history_id, start_date,
                               LEAD(start_date) OVER (ORDER BY start_date ASC, rental_amount_history_id ASC) AS next_start
                        FROM rental_amount_history
                        WHERE rental_id = @RentalId
                    )
                    UPDATE rah
                    SET end_date = CASE WHEN o.next_start IS NOT NULL THEN DATEADD(second, -1, o.next_start) ELSE NULL END
                    FROM rental_amount_history rah
                    JOIN Ordered o ON rah.rental_amount_history_id = o.rental_amount_history_id
                    WHERE rah.rental_id = @RentalId";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(fixQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@RentalId", rentalId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await rentalAmountHistoryService.NormalizeRentalAmountHistoryTransactionAsync(rentalId, connection, transaction);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await _clientMonthBalanceService.RebuildForRentalAsync(rentalId);

            await activityLogService.TryCreateActivityLogAsync(new ActivityLog
            {
                Action = "UPDATE",
                TableName = "rental_amount_history",
                RecordId = histId,
                OldValue = previousHistory == null ? null : JsonSerializer.Serialize(previousHistory),
                NewValue = JsonSerializer.Serialize(new { Id = histId, rentalId, amount, StartDate = newStartDate })
            });
        }

        public async Task DeleteClientRentalAmountEntryAsync(int clientId, int histId)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            if (histId <= 0) throw new ArgumentException("Invalid history ID.");

            // Verify and get rental info
            string verifyQuery = @"
                SELECT rah.rental_id
                FROM rental_amount_history rah
                JOIN rentals r ON rah.rental_id = r.rental_id
                WHERE rah.rental_amount_history_id = @HistId
                  AND r.client_id = @ClientId";
            var verifyParams = new[]
            {
                new Microsoft.Data.SqlClient.SqlParameter("@HistId", histId),
                new Microsoft.Data.SqlClient.SqlParameter("@ClientId", clientId)
            };
            var verifyTable = await accessDB.GetTableAsync("verify", verifyQuery, verifyParams);
            if (verifyTable.Rows.Count == 0) throw new InvalidOperationException("Tramo no encontrado o no pertenece al cliente.");
            int rentalId = Convert.ToInt32(verifyTable.Rows[0]["rental_id"]);
            RentalAmountHistoryItemDto? previousHistory = (await GetClientRentalAmountHistoryAsync(clientId))
                .FirstOrDefault(history => history.Id == histId);

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                // Delete the entry
                string deleteQuery = "DELETE FROM rental_amount_history WHERE rental_amount_history_id = @HistId";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(deleteQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@HistId", histId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Fix end_dates chain after deletion
                string fixQuery = @"
                    WITH Ordered AS (
                        SELECT rental_amount_history_id, start_date,
                               LEAD(start_date) OVER (ORDER BY start_date ASC, rental_amount_history_id ASC) AS next_start
                        FROM rental_amount_history
                        WHERE rental_id = @RentalId
                    )
                    UPDATE rah
                    SET end_date = CASE WHEN o.next_start IS NOT NULL THEN DATEADD(second, -1, o.next_start) ELSE NULL END
                    FROM rental_amount_history rah
                    JOIN Ordered o ON rah.rental_amount_history_id = o.rental_amount_history_id
                    WHERE rah.rental_id = @RentalId";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(fixQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@RentalId", rentalId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await _clientMonthBalanceService.RebuildForRentalAsync(rentalId);

            await activityLogService.TryCreateActivityLogAsync(new ActivityLog
            {
                Action = "DELETE",
                TableName = "rental_amount_history",
                RecordId = histId,
                OldValue = previousHistory == null ? null : JsonSerializer.Serialize(previousHistory),
                NewValue = JsonSerializer.Serialize(new { Deleted = true, rentalId })
            });
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
                    IsSixMonthPromotion = row["is_six_month_promotion"] != DBNull.Value && Convert.ToBoolean(row["is_six_month_promotion"]),
                InitialAmount = row["initial_amount"] != DBNull.Value ? Convert.ToDecimal(row["initial_amount"]) : null,
                NextIncreaseDay = row["increase_anchor_date"] != DBNull.Value ? Convert.ToDateTime(row["increase_anchor_date"]) : DateTime.MinValue,
                // --- FIN CAMPOS ACTUALIZADOS ---

                ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
                RentalId = row["rental_id"] != DBNull.Value ? Convert.ToInt32(row["rental_id"]) : null,
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
                CommentUpdatedAt = row["comment_updated_at"] != DBNull.Value ? Convert.ToDateTime(row["comment_updated_at"]) : null,
                DepartureStatus = row["departure_status"] != DBNull.Value ? row["departure_status"]?.ToString() : null
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
            if (!dto.LegacyNextIncreaseDate.HasValue)
                throw new ArgumentException("La fecha de próximo aumento es requerida.");

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
                            IsSixMonthPromotion = dto.IsLegacy6MonthPromo || dto.IsSixMonthPromotion,
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
                                await lockerService.UnassignLockersFromRentalTransactionAsync(currentRental.Id, lockersToRemove, connection, transaction);
                                await daoClient.CloseLockerHistoryTransactionAsync(id, lockersToRemove, connection, transaction);
                            }
                            if (lockersToAdd.Count != 0) {
                                foreach(var lockerIdToAdd in lockersToAdd) {
                                    // IsLockerAvailableAsync ya considera espacios libres (status != OCUPADO)
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
                            OldValue = JsonSerializer.Serialize(existingClient),
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
                                await lockerService.UnassignLockersFromRentalTransactionAsync(activeRentalId.Value, lockerIds, connection, transaction);
                                await daoClient.CloseLockerHistoryTransactionAsync(clientId, lockerIds, connection, transaction);
                                await rentalService.UpdateContractedM3TransactionAsync(activeRentalId.Value, 0m, connection, transaction);
                            }

                            await rentalAmountHistoryService.CloseOpenHistoriesByRentalIdTransactionAsync(activeRentalId.Value, today, connection, transaction);
                            
                            await rentalService.EndActiveRentalByClientIdTransactionAsync(clientId, today, connection, transaction);
                        }

                        await daoClient.DeactivateClientTransactionAsync(clientId, connection, transaction);

                        await activityLogService.CreateActivityLogTransactionAsync(new ActivityLog
                        {
                            Action = "DELETE",
                            TableName = "clients",
                            RecordId = clientId,
                            OldValue = JsonSerializer.Serialize(new { Active = true }),
                            NewValue = JsonSerializer.Serialize(new { Active = false })
                        }, connection, transaction);

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

        private static bool IsCashPaymentMethod(string? paymentMethodName)
        {
            return paymentMethodName?.Contains("efectivo", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static decimal RoundRentAmountForPaymentMethod(
            decimal targetAmount,
            string? paymentMethodName,
            decimal originalRent,
            decimal targetPercentage)
        {
            if (targetAmount == 0m)
                return 0m;

            // Sin aumento no se debe alterar el importe histórico. El guard de
            // originalRent evita una división inválida para alquileres sin base.
            if (targetPercentage <= 0m || originalRent <= 0m)
                return targetAmount;

            decimal step = IsCashPaymentMethod(paymentMethodName) ? 1000m : 100m;
            decimal rounded = Math.Ceiling(targetAmount / step) * step;
            decimal currentPercentage = ((rounded - originalRent) / originalRent) * 100m;

            // El redondeo nunca puede dejar el aumento efectivo por debajo del
            // porcentaje configurado en monthly_increase_settings.
            while (currentPercentage < targetPercentage)
            {
                rounded += step;
                currentPercentage = ((rounded - originalRent) / originalRent) * 100m;
            }

            return rounded;
        }

        private static decimal RoundProportionalAmountForPaymentMethod(
            decimal amount,
            string? paymentMethodName)
        {
            if (amount == 0m)
                return 0m;

            decimal step = IsCashPaymentMethod(paymentMethodName) ? 1000m : 100m;
            return Math.Round(amount / step, MidpointRounding.AwayFromZero) * step;
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

                        decimal newPaymentIdentifier;
                        if (dto.PaymentIdentifier.HasValue && dto.PaymentIdentifier.Value > 0)
                        {
                            if (await daoClient.ExistsByPaymentIdentifierAsync(dto.PaymentIdentifier.Value, clientId, connection, transaction))
                            {
                                throw new InvalidOperationException("Ya existe otro cliente activo con este Identificador de Pago.");
                            }
                            newPaymentIdentifier = dto.PaymentIdentifier.Value;
                        }
                        else
                        {
                            decimal maxIdentifier = await daoClient.GetMaxPaymentIdentifierAsync(connection, transaction);
                            newPaymentIdentifier = maxIdentifier + 0.01m;
                        }

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
                            IsSixMonthPromotion = dto.IsLegacy6MonthPromo || dto.IsSixMonthPromotion,
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
                                // IsLockerAvailableAsync ya considera espacios libres (status != OCUPADO)
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

        public async Task DeleteLockerHistoryAsync(int clientId, int historyId)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            if (historyId <= 0) throw new ArgumentException("Invalid history ID.");

            await daoClient.DeleteLockerHistoryAsync(clientId, historyId);
        }

        public async Task<bool> UpdateClientColorAsync(int clientId, string? color)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            bool updated = await daoClient.UpdateClientColorAsync(clientId, color);
            if (updated)
            {
                await activityLogService.TryCreateActivityLogAsync(new ActivityLog
                {
                    Action = "UPDATE",
                    TableName = "clients",
                    RecordId = clientId,
                    NewValue = JsonSerializer.Serialize(new { Field = "color", Value = color })
                });
            }
            return updated;
        }

        public async Task ApplyDepartureActionAsync(int clientId, ClientDepartureActionDto request)
        {
            if (clientId <= 0) throw new ArgumentException("ID de cliente inválido.");
            ArgumentNullException.ThrowIfNull(request);

            string action = request.Action?.Trim().ToUpperInvariant() ?? string.Empty;
            if (action is not ("SE_VA" or "SE_QUEDA" or "DAR_DE_BAJA"))
                throw new ArgumentException("La acción de salida del cliente no es válida.");

            string? surchargeAction = request.PendingSurchargeAction?.Trim().ToLowerInvariant();
            if (surchargeAction is not (null or "forgive" or "immediate"))
                throw new ArgumentException("La acción del recargo pendiente no es válida.");

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var rentalData = await GetActiveRentalDepartureDataAsync(clientId, connection, transaction);
                if (rentalData.ClientExists == false)
                    throw new KeyNotFoundException("No se encontró el cliente.");

                DateTime? departureDate = request.DepartureDate?.Date;
                if (request.ChargeProportional)
                {
                    if (!departureDate.HasValue)
                        throw new ArgumentException("La fecha de salida es obligatoria para calcular el proporcional.");

                    DateTime nextMonth = new DateTime(rentalData.Today.Year, rentalData.Today.Month, 1).AddMonths(1);
                    if (departureDate.Value.Year != nextMonth.Year || departureDate.Value.Month != nextMonth.Month)
                        throw new ArgumentException("La fecha de salida debe pertenecer al mes siguiente.");
                }

                // La tabla y el popover muestran el importe que corresponde al mes
                // de salida (incluyendo un aumento ya programado). No usar solamente
                // el importe vigente hoy, porque puede dejar un débito proporcional
                // distinto al importe que el usuario confirmó.
                decimal proportionalRent = rentalData.CurrentRent;
                if (request.ChargeProportional && rentalData.ActiveRentalId.HasValue && departureDate.HasValue)
                {
                    proportionalRent = await GetRentalAmountForMonthAsync(
                        rentalData.ActiveRentalId.Value,
                        departureDate.Value,
                        rentalData.CurrentRent,
                        rentalData.PaymentMethodName,
                        connection,
                        transaction);
                }

                if (action == "SE_QUEDA")
                {
                    if (!rentalData.ActiveRentalId.HasValue)
                        throw new InvalidOperationException("El cliente no tiene un alquiler activo para volver a ocupar sus bauleras.");

                    if (string.Equals(rentalData.DepartureStatus?.Trim(), "SE_VA", StringComparison.OrdinalIgnoreCase))
                    {
                        await RestoreNextMonthDebitForReturningClientAsync(
                            rentalData.ActiveRentalId.Value,
                            rentalData.CurrentRent,
                            rentalData.Today,
                            request.RestoreProportional,
                            rentalData.PaymentMethodName,
                            connection,
                            transaction);
                    }

                    await UpdateClientDepartureStatusAsync(clientId, null, connection, transaction);
                    await SetAssignedLockerStatusAsync(rentalData.ActiveRentalId.Value, "OCUPADO", connection, transaction);
                }
                else
                {
                    if (rentalData.ActiveRentalId.HasValue)
                    {
                        // Un proporcional del mes siguiente reemplaza al débito mensual completo.
                        if (request.RemoveNextMonthDebit || request.ChargeProportional)
                        {
                            await RemoveNextMonthDebitAsync(rentalData.ActiveRentalId.Value, rentalData.Today, connection, transaction);
                        }

                        if (request.ChargeProportional && proportionalRent > 0m && departureDate.HasValue)
                        {
                            await ApplyDepartureProportionalDebitAsync(
                                rentalData.ActiveRentalId.Value,
                                proportionalRent,
                                departureDate.Value,
                                rentalData.Today,
                                rentalData.PaymentMethodName,
                                connection,
                                transaction);
                        }

                        if (rentalData.PendingSurcharge > 0m && surchargeAction == "forgive")
                        {
                            await ResetPendingSurchargeAsync(rentalData.ActiveRentalId.Value, connection, transaction);
                        }
                        else if (rentalData.PendingSurcharge > 0m && surchargeAction == "immediate")
                        {
                            await ChargePendingSurchargeImmediatelyAsync(rentalData.ActiveRentalId.Value, rentalData.PendingSurcharge, rentalData.Today, connection, transaction);
                        }

                        if (action == "SE_VA")
                        {
                            await UpdateClientDepartureStatusAsync(clientId, "SE_VA", connection, transaction);
                            await SetAssignedLockerStatusAsync(rentalData.ActiveRentalId.Value, "POR LIBERARSE", connection, transaction);
                        }
                        else
                        {
                            var lockerIds = await lockerService.GetLockerIdsByRentalIdTransactionAsync(rentalData.ActiveRentalId.Value, connection, transaction);
                            if (lockerIds != null && lockerIds.Count > 0)
                            {
                                await lockerService.UnassignLockersFromRentalTransactionAsync(rentalData.ActiveRentalId.Value, lockerIds, connection, transaction);
                                await daoClient.CloseLockerHistoryTransactionAsync(clientId, lockerIds, connection, transaction);
                                await rentalService.UpdateContractedM3TransactionAsync(rentalData.ActiveRentalId.Value, 0m, connection, transaction);
                            }

                            await rentalAmountHistoryService.CloseOpenHistoriesByRentalIdTransactionAsync(rentalData.ActiveRentalId.Value, rentalData.Today, connection, transaction);
                            await rentalService.EndActiveRentalByClientIdTransactionAsync(clientId, rentalData.Today, connection, transaction);
                            await UpdateClientDepartureStatusAsync(clientId, null, connection, transaction);
                        }

                        await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rentalData.ActiveRentalId.Value, connection, transaction);
                    }
                    else if (action == "SE_VA")
                    {
                        throw new InvalidOperationException("El cliente no tiene un alquiler activo para marcarlo como SE VA.");
                    }
                    else
                    {
                        await UpdateClientDepartureStatusAsync(clientId, null, connection, transaction);
                    }

                    if (action == "DAR_DE_BAJA")
                    {
                        await daoClient.DeactivateClientTransactionAsync(clientId, connection, transaction);
                    }
                }

                if (action == "SE_QUEDA" && rentalData.ActiveRentalId.HasValue)
                {
                    await _clientMonthBalanceService.RebuildForRentalTransactionAsync(
                        rentalData.ActiveRentalId.Value,
                        connection,
                        transaction);
                }

                await activityLogService.CreateActivityLogTransactionAsync(new ActivityLog
                {
                    Action = action == "SE_QUEDA" ? "CLIENT_STAYS" : action == "SE_VA" ? "CLIENT_DEPARTURE" : "DELETE",
                    TableName = "clients",
                    RecordId = clientId,
                    OldValue = JsonSerializer.Serialize(new { DepartureStatus = rentalData.DepartureStatus, Active = rentalData.IsActive }),
                    NewValue = JsonSerializer.Serialize(new { DepartureStatus = action == "SE_VA" ? "SE_VA" : (string?)null, Active = action != "DAR_DE_BAJA" })
                }, connection, transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ClientDepartureProportionalPreviewDto> GetDepartureProportionalPreviewAsync(int clientId, DateTime departureDate)
        {
            if (clientId <= 0) throw new ArgumentException("ID de cliente inválido.");

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var rentalData = await GetActiveRentalDepartureDataAsync(clientId, connection, transaction);
                if (!rentalData.ClientExists)
                    throw new KeyNotFoundException("No se encontró el cliente.");

                if (!rentalData.IsActive || !rentalData.ActiveRentalId.HasValue)
                    throw new InvalidOperationException("El cliente no tiene un alquiler activo para calcular el proporcional.");

                DateTime normalizedDepartureDate = departureDate.Date;
                DateTime nextMonth = new DateTime(rentalData.Today.Year, rentalData.Today.Month, 1).AddMonths(1);
                if (normalizedDepartureDate.Year != nextMonth.Year || normalizedDepartureDate.Month != nextMonth.Month)
                    throw new ArgumentException("La fecha de salida debe pertenecer al mes siguiente.");

                decimal baseRent = await GetRentalAmountForMonthAsync(
                    rentalData.ActiveRentalId.Value,
                    normalizedDepartureDate,
                    rentalData.CurrentRent,
                    rentalData.PaymentMethodName,
                    connection,
                    transaction);
                int daysInMonth = DateTime.DaysInMonth(normalizedDepartureDate.Year, normalizedDepartureDate.Month);
                int daysToCharge = normalizedDepartureDate.Day;

                await transaction.RollbackAsync();

                return new ClientDepartureProportionalPreviewDto
                {
                    BaseRent = baseRent,
                    ProportionalAmount = RoundProportionalAmountForPaymentMethod(
                        baseRent / daysInMonth * daysToCharge,
                        rentalData.PaymentMethodName),
                    DaysToCharge = daysToCharge,
                    DaysInMonth = daysInMonth
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private sealed class ActiveRentalDepartureData
        {
            public bool ClientExists { get; init; }
            public bool IsActive { get; init; }
            public string? DepartureStatus { get; init; }
            public int? ActiveRentalId { get; init; }
            public decimal CurrentRent { get; init; }
            public decimal PendingSurcharge { get; init; }
            public string PaymentMethodName { get; init; } = string.Empty;
            public DateTime Today { get; init; }
        }

        private async Task<ActiveRentalDepartureData> GetActiveRentalDepartureDataAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                SELECT TOP 1
                    c.active,
                    c.departure_status,
                    r.rental_id,
                    ISNULL(rah.amount, 0) AS current_rent,
                    ISNULL(r.pending_surcharge, 0) AS pending_surcharge,
                    ISNULL(pm.name, '') AS payment_method_name
                FROM clients c
                LEFT JOIN payment_methods pm ON c.preferred_payment_method_id = pm.payment_method_id
                OUTER APPLY (
                    SELECT TOP 1 r1.*
                    FROM rentals r1
                    WHERE r1.client_id = c.client_id AND r1.active = 1
                    ORDER BY r1.start_date DESC, r1.rental_id DESC
                ) r
                OUTER APPLY (
                    SELECT TOP 1 h.amount
                    FROM rental_amount_history h
                    WHERE h.rental_id = r.rental_id
                      AND h.start_date <= DATEADD(hour, -3, GETUTCDATE())
                    ORDER BY h.start_date DESC, h.rental_amount_history_id DESC
                ) rah
                WHERE c.client_id = @client_id AND c.is_deleted = 0;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId });
            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return new ActiveRentalDepartureData { ClientExists = false };

            return new ActiveRentalDepartureData
            {
                ClientExists = true,
                IsActive = !reader.IsDBNull(0) && reader.GetBoolean(0),
                DepartureStatus = reader.IsDBNull(1) ? null : reader.GetString(1),
                ActiveRentalId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                CurrentRent = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                PendingSurcharge = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                PaymentMethodName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Today = TimeHelper.GetArgentinaTime().Date
            };
        }

        private async Task<decimal> GetRentalAmountForMonthAsync(
            int rentalId,
            DateTime effectiveDate,
            decimal fallbackAmount,
            string? paymentMethodName,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            DateTime monthStart = new DateTime(effectiveDate.Year, effectiveDate.Month, 1);
            DateTime nextMonthStart = monthStart.AddMonths(1);

            const string query = @"
                SELECT TOP 1 amount, start_date
                FROM rental_amount_history
                WHERE rental_id = @rental_id
                  AND start_date < @next_month_start
                  AND (end_date IS NULL OR end_date >= @month_start)
                ORDER BY start_date DESC, rental_amount_history_id DESC;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            command.Parameters.Add(new SqlParameter("@month_start", SqlDbType.Date) { Value = monthStart });
            command.Parameters.Add(new SqlParameter("@next_month_start", SqlDbType.Date) { Value = nextMonthStart });

            decimal monthAmount = fallbackAmount;
            DateTime? historyStartDate = null;
            using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow))
            {
                if (await reader.ReadAsync())
                {
                    monthAmount = reader.IsDBNull(0)
                        ? fallbackAmount
                        : Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture);
                    historyStartDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                }
            }

            // A history row that starts in the target month is already the
            // effective/planned amount for that month. Do not apply the global
            // setting a second time on top of it.
            if (historyStartDate.HasValue
                && historyStartDate.Value >= monthStart
                && historyStartDate.Value < nextMonthStart)
            {
                return monthAmount;
            }

            const string rentalQuery = @"
                SELECT increase_anchor_date, price_lock_end_date
                FROM rentals
                WHERE rental_id = @rental_id;";

            DateTime? increaseAnchorDate = null;
            DateTime? priceLockEndDate = null;
            using (var rentalCommand = new SqlCommand(rentalQuery, connection, transaction))
            {
                rentalCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                using var rentalReader = await rentalCommand.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (!await rentalReader.ReadAsync())
                    return monthAmount;

                increaseAnchorDate = rentalReader.IsDBNull(0) ? null : rentalReader.GetDateTime(0);
                priceLockEndDate = rentalReader.IsDBNull(1) ? null : rentalReader.GetDateTime(1);
            }

            bool isIncreaseMonth = increaseAnchorDate.HasValue
                && increaseAnchorDate.Value.Year == monthStart.Year
                && increaseAnchorDate.Value.Month == monthStart.Month;
            bool isPriceLocked = priceLockEndDate.HasValue
                && priceLockEndDate.Value.Date > monthStart.Date;

            if (!isIncreaseMonth || isPriceLocked)
                return monthAmount;

            decimal? percentage = await _daoMonthlyIncrease.GetIncreasePercentageForMonthAsync(
                monthStart,
                connection,
                transaction);
            if (!percentage.HasValue || percentage.Value <= 0m)
                return monthAmount;

            // Keep the same payment-method-specific rounding rule used by the
            // increase flow before calculating the daily proportional amount.
            decimal targetAmount = monthAmount * (1m + percentage.Value / 100m);
            return RoundRentAmountForPaymentMethod(
                targetAmount,
                paymentMethodName,
                monthAmount,
                percentage.Value);
        }

        private async Task RestoreNextMonthDebitForReturningClientAsync(
            int rentalId,
            decimal fallbackRent,
            DateTime today,
            bool restoreProportional,
            string? paymentMethodName,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            DateTime nextMonth = new DateTime(today.Year, today.Month, 1).AddMonths(1);
            var culture = new CultureInfo("es-AR");
            string monthTitle = culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(nextMonth.Month));
            string rentConceptPrefix = $"Alquiler {monthTitle} {nextMonth.Year}";
            string proportionalConceptPrefix = $"{rentConceptPrefix} (Proporcional salida";

            // La decisión debe usar el estado real del ledger, no un saldo mensual
            // que pudiera haber quedado desactualizado antes de volver a marcarlo
            // como SE QUEDA.
            await _clientMonthBalanceService.RebuildForRentalTransactionAsync(
                rentalId,
                connection,
                transaction);
            bool hasUnpaidPreviousMonth = await HasUnpaidMonthBeforeAsync(
                rentalId,
                nextMonth,
                connection,
                transaction);

            bool hasProportionalDebit = await HasDebitWithConceptPrefixAsync(
                rentalId,
                proportionalConceptPrefix,
                connection,
                transaction);

            if (hasProportionalDebit && restoreProportional)
            {
                await RemoveDebitWithConceptPrefixAsync(
                    rentalId,
                    proportionalConceptPrefix,
                    connection,
                    transaction);
            }

            bool hasFullMonthlyDebit = await HasFullMonthlyDebitAsync(
                rentalId,
                rentConceptPrefix,
                proportionalConceptPrefix,
                connection,
                transaction);

            // Si el usuario eligió conservar el proporcional, ese movimiento ya
            // representa el cobro del mes y no se agrega otro débito encima.
            // Si hay deuda anterior, quitar el proporcional no debe adelantar un
            // débito completo: el último mes adeudado sigue siendo el anterior.
            bool shouldCreateFullDebit = !hasUnpaidPreviousMonth
                && !hasFullMonthlyDebit
                && (!hasProportionalDebit || restoreProportional);
            if (!shouldCreateFullDebit)
                return;

            decimal nextMonthRent = await GetRentalAmountForMonthAsync(
                rentalId,
                nextMonth,
                fallbackRent,
                paymentMethodName,
                connection,
                transaction);
            if (nextMonthRent <= 0m)
                return;

            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
            {
                RentalId = rentalId,
                MovementDate = nextMonth,
                MovementType = "DEBITO",
                Concept = rentConceptPrefix,
                Amount = nextMonthRent,
                PaymentId = null
            }, connection, transaction);
        }

        private static async Task<bool> HasDebitWithConceptPrefixAsync(
            int rentalId,
            string conceptPrefix,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM account_movements
                    WHERE rental_id = @rental_id
                      AND movement_type = 'DEBITO'
                      AND LTRIM(RTRIM(ISNULL(concept, ''))) COLLATE Latin1_General_100_CI_AI LIKE @concept_prefix + '%'
                ) THEN 1 ELSE 0 END;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            command.Parameters.Add(new SqlParameter("@concept_prefix", SqlDbType.NVarChar, 200) { Value = conceptPrefix });
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        private static async Task<bool> HasFullMonthlyDebitAsync(
            int rentalId,
            string rentConceptPrefix,
            string proportionalConceptPrefix,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM account_movements
                    WHERE rental_id = @rental_id
                      AND movement_type = 'DEBITO'
                      AND LTRIM(RTRIM(ISNULL(concept, ''))) COLLATE Latin1_General_100_CI_AI LIKE @rent_concept_prefix + '%'
                      AND LTRIM(RTRIM(ISNULL(concept, ''))) COLLATE Latin1_General_100_CI_AI NOT LIKE @proportional_concept_prefix + '%'
                ) THEN 1 ELSE 0 END;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            command.Parameters.Add(new SqlParameter("@rent_concept_prefix", SqlDbType.NVarChar, 200) { Value = rentConceptPrefix });
            command.Parameters.Add(new SqlParameter("@proportional_concept_prefix", SqlDbType.NVarChar, 200) { Value = proportionalConceptPrefix });
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        private static async Task<bool> HasUnpaidMonthBeforeAsync(
            int rentalId,
            DateTime monthStart,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM client_month_balances
                    WHERE rental_id = @rental_id
                      AND TRY_CONVERT(date, CONCAT('01/', LTRIM(RTRIM(month_year))), 103) < @month_start
                      AND ISNULL(balance, 0) - ISNULL(paid, 0) - ISNULL(advanced_payment, 0) > 0
                ) THEN 1 ELSE 0 END;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            command.Parameters.Add(new SqlParameter("@month_start", SqlDbType.Date) { Value = monthStart });
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        private static async Task RemoveDebitWithConceptPrefixAsync(
            int rentalId,
            string conceptPrefix,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string query = @"
                DELETE FROM account_movements
                WHERE rental_id = @rental_id
                  AND movement_type = 'DEBITO'
                  AND LTRIM(RTRIM(ISNULL(concept, ''))) COLLATE Latin1_General_100_CI_AI LIKE @concept_prefix + '%';";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            command.Parameters.Add(new SqlParameter("@concept_prefix", SqlDbType.NVarChar, 200) { Value = conceptPrefix });
            await command.ExecuteNonQueryAsync();
        }

        private static async Task UpdateClientDepartureStatusAsync(int clientId, string? status, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "UPDATE clients SET departure_status = @status WHERE client_id = @client_id";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@status", SqlDbType.VarChar, 20) { Value = (object?)status ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId });
            await command.ExecuteNonQueryAsync();
        }

        private static async Task SetAssignedLockerStatusAsync(int rentalId, string status, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "UPDATE lockers SET status = @status WHERE rental_id = @rental_id AND active = 1";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@status", SqlDbType.VarChar, 50) { Value = status });
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            await command.ExecuteNonQueryAsync();
        }

        private static async Task RemoveNextMonthDebitAsync(int rentalId, DateTime today, SqlConnection connection, SqlTransaction transaction)
        {
            DateTime nextMonth = new DateTime(today.Year, today.Month, 1).AddMonths(1);
            var culture = new CultureInfo("es-AR");
            string monthTitle = culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(nextMonth.Month));
            string conceptPrefix = $"Alquiler {monthTitle} {nextMonth.Year}";

            const string query = @"
                DELETE FROM account_movements
                WHERE rental_id = @rental_id
                  AND movement_type = 'DEBITO'
                  AND @today < @next_month
                  AND LTRIM(RTRIM(ISNULL(concept, ''))) COLLATE Latin1_General_100_CI_AI LIKE @concept_prefix + '%';";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            command.Parameters.Add(new SqlParameter("@today", SqlDbType.Date) { Value = today });
            command.Parameters.Add(new SqlParameter("@next_month", SqlDbType.Date) { Value = nextMonth });
            command.Parameters.Add(new SqlParameter("@concept_prefix", SqlDbType.NVarChar, 200) { Value = conceptPrefix });
            await command.ExecuteNonQueryAsync();
        }

        private async Task ApplyDepartureProportionalDebitAsync(
            int rentalId,
            decimal currentRent,
            DateTime departureDate,
            DateTime movementDate,
            string? paymentMethodName,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            int daysInMonth = DateTime.DaysInMonth(departureDate.Year, departureDate.Month);
            int daysToCharge = departureDate.Day;
            decimal amount = RoundProportionalAmountForPaymentMethod(
                currentRent / daysInMonth * daysToCharge,
                paymentMethodName);
            if (amount <= 0m) return;

            var culture = new CultureInfo("es-AR");
            string monthTitle = culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(departureDate.Month));

            // Si la operación se confirma nuevamente, reemplazar el proporcional
            // automático anterior evita duplicar el cargo del mes siguiente.
            const string deletePreviousQuery = @"
                DELETE FROM account_movements
                WHERE rental_id = @rental_id
                  AND movement_type = 'DEBITO'
                  AND payment_id IS NULL
                  AND LTRIM(RTRIM(ISNULL(concept, ''))) COLLATE Latin1_General_100_CI_AI
                      LIKE @concept_prefix + '%';";
            using (var deleteCommand = new SqlCommand(deletePreviousQuery, connection, transaction))
            {
                deleteCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                deleteCommand.Parameters.Add(new SqlParameter("@concept_prefix", SqlDbType.NVarChar, 200)
                {
                    Value = $"Alquiler {monthTitle} {departureDate.Year} (Proporcional salida"
                });
                await deleteCommand.ExecuteNonQueryAsync();
            }

            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
            {
                RentalId = rentalId,
                MovementDate = movementDate,
                MovementType = "DEBITO",
                Concept = $"Alquiler {monthTitle} {departureDate.Year} (Proporcional salida {daysToCharge} días)",
                Amount = amount,
                PaymentId = null
            }, connection, transaction);
        }

        private static async Task ResetPendingSurchargeAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"UPDATE rentals SET pending_surcharge = 0, pending_surcharge_rent_base = NULL, pending_surcharge_period = NULL WHERE rental_id = @rental_id";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            await command.ExecuteNonQueryAsync();
        }

        private async Task ChargePendingSurchargeImmediatelyAsync(int rentalId, decimal amount, DateTime today, SqlConnection connection, SqlTransaction transaction)
        {
            if (amount <= 0m) return;
            var culture = new CultureInfo("es-AR");
            string monthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(today.Month));
            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement
            {
                RentalId = rentalId,
                MovementDate = today,
                MovementType = "DEBITO",
                Concept = $"Interés por mora de {monthTitle} {today.Year} (cobrado en el acto por baja)",
                Amount = amount,
                PaymentId = null
            }, connection, transaction);
            await ResetPendingSurchargeAsync(rentalId, connection, transaction);
        }

        public async Task<bool> UpdateClientCommentAsync(int clientId, string? comment)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            bool updated = await daoClient.UpdateClientCommentAsync(clientId, comment);
            if (updated)
            {
                await activityLogService.TryCreateActivityLogAsync(new ActivityLog
                {
                    Action = "UPDATE",
                    TableName = "clients",
                    RecordId = clientId,
                    NewValue = JsonSerializer.Serialize(new { Field = "comment", Value = comment })
                });
            }
            return updated;
        }

        public async Task<bool> UpdateClientNotesAsync(int clientId, string? notes)
        {
            if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
            bool updated = await daoClient.UpdateClientNotesAsync(clientId, notes);
            if (updated)
            {
                await activityLogService.TryCreateActivityLogAsync(new ActivityLog
                {
                    Action = "UPDATE",
                    TableName = "clients",
                    RecordId = clientId,
                    NewValue = JsonSerializer.Serialize(new { Field = "notes", Value = notes })
                });
            }
            return updated;
        }

        public async Task<decimal> GetNextPaymentIdentifierAsync()
        {
            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            decimal maxIdentifier = await daoClient.GetMaxPaymentIdentifierAsync(connection, transaction);
            return maxIdentifier + 0.01m;
        }

        public async Task<bool> CheckPaymentIdentifierExistsAsync(decimal identifier, int? excludeClientId = null)
        {
            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            
            if (excludeClientId.HasValue)
            {
                return await daoClient.ExistsByPaymentIdentifierAsync(identifier, excludeClientId.Value, connection, transaction);
            }
            return await daoClient.ExistsByPaymentIdentifierAsync(identifier, connection, transaction);
        }
    }
}
