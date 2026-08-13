using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Dtos.AccountMovement;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using Microsoft.Extensions.DependencyInjection;
using GuardeSoftwareAPI.Services.payment;
using GuardeSoftwareAPI.Services.rentalAmountHistory;

namespace GuardeSoftwareAPI.Services.accountMovement {

    public class AccountMovementService : IAccountMovementService
    {
        private readonly DaoAccountMovement _daoAccountMovement;
        private readonly DaoRental _daoRental;
        private readonly ILogger<IAccountMovementService> _logger;
        private readonly AccessDB accessDB;
        private readonly IClientMonthBalanceService _clientMonthBalanceService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IRentalAmountHistoryService _rentalAmountHistoryService;

        public AccountMovementService(AccessDB _accessDB, ILogger<AccountMovementService> logger, IClientMonthBalanceService clientMonthBalanceService, IServiceProvider serviceProvider, IRentalAmountHistoryService rentalAmountHistoryService)
        {
            _daoAccountMovement = new DaoAccountMovement(_accessDB);
            _daoRental = new DaoRental(_accessDB);
            _logger = logger;
            accessDB = _accessDB;
            _clientMonthBalanceService = clientMonthBalanceService;
            _serviceProvider = serviceProvider;
            _rentalAmountHistoryService = rentalAmountHistoryService;
        }

        public async Task<List<AccountMovement>> GetAccountMovementList()
        {

            DataTable AccountsTable = await _daoAccountMovement.GetAccountMovement();
            List<AccountMovement> Accounts = new List<AccountMovement>();

            foreach (DataRow row in AccountsTable.Rows)
            {
                int idAccountMovement = (int)row["movement_id"];

                AccountMovement accountmovement = new AccountMovement
                {
                    Id = idAccountMovement,

                    RentalId = row["rental_id"] != DBNull.Value
                    ? (int)row["rental_id"] : 0,

                    MovementDate = (DateTime)row["movement_date"],

                    MovementType = row["movement_type"]?.ToString() ?? string.Empty,

                    Concept = row["concept"]?.ToString() ?? string.Empty,

                    Amount = row["amount"] != DBNull.Value
                    ? Convert.ToDecimal(row["amount"])
                    : 0m,

                    PaymentId = row["payment_id"] != DBNull.Value
                    ? (int)row["payment_id"] : 0,

                };
                Accounts.Add(accountmovement);
            }
            return Accounts;

        }

        public async Task<List<AccountMovement>> GetAccountMovementListByRentalId(int id)
        {
            DataTable AccountsTable = await _daoAccountMovement.GetAccountMovByRentalId(id);
            List<AccountMovement> Accounts = new List<AccountMovement>();

            foreach (DataRow row in AccountsTable.Rows)
            {
                int idAccountMovement = (int)row["movement_id"];

                AccountMovement accountmovement = new AccountMovement
                {
                    Id = idAccountMovement,

                    RentalId = row["rental_id"] != DBNull.Value
                    ? (int)row["rental_id"] : 0,

                    MovementDate = (DateTime)row["movement_date"],

                    MovementType = row["movement_type"]?.ToString() ?? string.Empty,

                    Concept = row["concept"]?.ToString() ?? string.Empty,

                    Amount = row["amount"] != DBNull.Value
                    ? Convert.ToDecimal(row["amount"])
                    : 0m,

                    PaymentId = row["payment_id"] != DBNull.Value
                    ? (int)row["payment_id"] : 0,

                };
                Accounts.Add(accountmovement);
            }
            return Accounts;

        }

        public async Task<bool> CreateAccountMovement(AccountMovement accountMovement)
        {

            if (accountMovement == null)
                throw new ArgumentNullException(nameof(accountMovement));

            if (accountMovement.RentalId <= 0)
                throw new ArgumentException("invalid rental ID.");

            if (string.IsNullOrWhiteSpace(accountMovement.MovementType))
                throw new ArgumentException("MovementType required.");

            if (accountMovement.Amount <= 0)
                throw new ArgumentException("Amount must to be > 0");

            if (await _daoAccountMovement.CreateAccountMovement(accountMovement)) return true;
            else return false;
        }

        public async Task<bool> CreateAccountMovementTransactionAsync(AccountMovement accountMovement, SqlConnection connection, SqlTransaction transaction)
        {
            if (accountMovement == null)
                throw new ArgumentNullException(nameof(accountMovement));

            if (accountMovement.RentalId <= 0)
                throw new ArgumentException("Invalid rental ID.");

            if (string.IsNullOrWhiteSpace(accountMovement.MovementType))
                throw new ArgumentException("MovementType required.");

            if (accountMovement.Amount < 0)
                throw new ArgumentException("Amount must be > 0");

            return await _daoAccountMovement.CreateAccountMovementTransactionAsync(accountMovement, connection, transaction);
        }

        public async Task ApplyMonthlyDebitsAsync()
        {
            _logger.LogInformation("--- Iniciando Job Aplicador de Débitos Mensuales ---");

            // Obtenemos los IDs de alquileres activos
            // Nota: Asegúrate de que este método en DaoRental no use conexión cerrada internamente
            // si planeas reutilizar conexiones, pero aquí lo llamamos aparte.
            var activeRentalIds = await _daoRental.GetActiveRentalsIdsAsync();
            
            _logger.LogInformation($"Se encontraron {activeRentalIds.Count} alquileres activos para procesar.");

            int skippedCount = 0;
            int duplicateCount = 0;
            int processedCount = 0;

            // Preparamos los datos del mes ACTUAL (el que queremos cobrar)
            var culture = new CultureInfo("es-AR");
            string monthName = culture.DateTimeFormat.GetMonthName(DateTime.Now.Month);
            // Usamos culture.TextInfo para capitalizar correctamente en español
            string titleMonth = culture.TextInfo.ToTitleCase(monthName); 
            string currentYear = DateTime.Now.Year.ToString();

            // Concepto Base que buscaremos: "Alquiler Febrero 2025"
            // El DAO buscará con LIKE 'Alquiler Febrero 2025%' para cubrir variantes
            string targetConceptBase = $"Alquiler {titleMonth} {currentYear}"; 

            
            // Procesamos cada rental individualmente
            foreach (var rentalId in activeRentalIds)
            {
                // Abrimos una conexión POR CADA rental para aislar fallos y transacciones
                using (var connection = accessDB.GetConnectionClose())
                {
                    try
                    {
                        await connection.OpenAsync();
                        using var transaction = connection.BeginTransaction();

                        // 1. Verificar si ya existe un débito con este CONCEPTO (Corrección clave)
                        bool debitExists = await _daoAccountMovement.IsDebitAlreadyCreatedAsync(rentalId, targetConceptBase, connection, transaction);
                        
                        if (debitExists)
                        {
                            _logger.LogDebug($"Débito omitido para Rental ID {rentalId}: Ya existe un movimiento con concepto '{targetConceptBase}'.");
                            duplicateCount++;
                            await transaction.CommitAsync();
                            continue;
                        }

                        // 2. Obtener balance actual y monto de alquiler (usando la conexión)
                        // decimal currentBalance = await _daoRental.GetBalanceByRentalIdAsync(rentalId, connection);
                        decimal currentAmount = await _daoRental.GetCurrentRentAmountAsync(rentalId, connection, transaction);

                        _logger.LogDebug($"Rental ID {rentalId}:, Monto alquiler={currentAmount:C}");

                        if (currentAmount <= 0)
                        {
                            _logger.LogWarning($"El monto de alquiler para Rental ID {rentalId} es cero o negativo ({currentAmount:C}). Omitiendo débito.");
                            await transaction.CommitAsync();
                            continue;
                        }

                        // 3. Decidir si aplicar débito (Lógica de Crédito a favor)
                        // Si el balance + el nuevo débito sigue siendo negativo (o cero), significa que tiene saldo a favor suficiente.
                        // Ejemplo: Balance -10000, Nuevo Débito 5000 -> -5000 (Sigue teniendo crédito, no generamos deuda nueva, pero ¿debemos registrar el movimiento?)
                        // NOTA: Generalmente SÍ se debe registrar el movimiento de débito para que quede constancia en el histórico
                        // de que se "gastó" ese saldo a favor.
                        // Si tu lógica de negocio es "No generar movimiento si tiene saldo a favor", mantén el if.
                        // Si tu lógica es "Generar movimiento y que el saldo se reduzca", BORRA este bloque if.
                        /* if (currentBalance + currentAmount <= 0)
                        {
                            _logger.LogInformation($"Rental ID {rentalId} tiene suficiente crédito ({currentBalance:C}) para cubrir el débito de {currentAmount:C}. Omitiendo débito este mes.");
                            skippedCount++;
                            continue; 
                        }
                        */

                        // 4. Crear objeto débito
                        var debitMovement = new AccountMovement
                        {
                            RentalId = rentalId,
                            MovementDate = DateTime.Now,
                            MovementType = "DEBITO",
                            Amount = currentAmount,
                            Concept = targetConceptBase, // Usamos el concepto estandarizado
                            PaymentId = null
                        };

                        // 5. Crear débito en BD
                        await _daoAccountMovement.CreateAccountMovementTransactionAsync(debitMovement, connection, transaction);
                        await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rentalId, connection, transaction);
                        await transaction.CommitAsync();
                        
                        _logger.LogInformation($"Débito de {currentAmount:C} creado para Rental ID {rentalId}. Concepto: {targetConceptBase}");
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error procesando Rental ID {rentalId} en ApplyMonthlyDebits: {ex.Message}");
                        // Continuar con el siguiente rental a pesar del error
                    }
                }
            }

            _logger.LogInformation($"--- Job finalizado. Procesados: {processedCount}, Ya existentes: {duplicateCount}, Omitidos por crédito: {skippedCount} ---");
        }

        public async Task<List<AccountMovement>> GetAccountMovementListByClientIdAsync(int clientId)
        {
            // 1. Encontrar todos los alquileres (activos o inactivos) para este cliente
            DataTable rentalTable = await _daoRental.GetRentalsByClientIdIncludingInactiveAsync(clientId);

            if (rentalTable.Rows.Count == 0)
            {
                _logger.LogWarning($"No se encontró ningún alquiler (rental) para el cliente ID {clientId}.");
                return new List<AccountMovement>(); // Devolver lista vacía
            }

            var movements = new List<AccountMovement>();
            foreach (DataRow row in rentalTable.Rows)
            {
                int rentalId = Convert.ToInt32(row["rental_id"]);
                var rentalMovements = await GetAccountMovementListByRentalId(rentalId);
                movements.AddRange(rentalMovements);
            }

            // Ordenamos por fecha de movimiento descendente
            movements.Sort((a, b) => b.MovementDate.CompareTo(a.MovementDate));
            return movements;
        }

        public async Task<bool> DeleteAccountMovementAsync(int movementId)
        {
            // 1. Buscamos el movimiento antes de borrarlo para extraer sus metadatos
            DataTable movTable = await _daoAccountMovement.GetAccountMovById(movementId);
            if (movTable.Rows.Count == 0)
            {
                _logger.LogWarning($"No se encontró el movimiento ID {movementId} para eliminar.");
                return false; 
            }

            DataRow row = movTable.Rows[0];
            int? paymentId = row["payment_id"] != DBNull.Value ? (int)row["payment_id"] : null;
            string movementType = row["movement_type"].ToString();
            string concept = row["concept"]?.ToString() ?? string.Empty;
            bool isPlannedRentDebit = movementType.Equals("DEBITO", StringComparison.OrdinalIgnoreCase)
                && !paymentId.HasValue
                && concept.EndsWith("(Planificado)", StringComparison.OrdinalIgnoreCase);
            
            // ¡NUEVO! Rescatamos a qué alquiler pertenece para recalcular después
            int rentalId = Convert.ToInt32(row["rental_id"]);

            // 2. Si es un ingreso de dinero (CREDITO) asociado a un pago formal, delegamos a IPaymentService para eliminar el pago completo en cascada
            if (paymentId.HasValue && paymentId > 0 && movementType == "CREDITO")
            {
                _logger.LogInformation($"El movimiento ID {movementId} (CREDITO) está asociado al pago ID {paymentId}. Eliminando el pago completo en cascada desde IPaymentService.");
                using var scope = _serviceProvider.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                return await paymentService.DeletePaymentAsync(movementId);
            }

            // 3. Borramos y reconstruimos dentro de una sola transacción. Así el
            // historial de abonos y los balances nunca quedan en estados distintos.
            _logger.LogInformation($"Eliminando movimiento ID {movementId}.");
            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                bool deleted = await _daoAccountMovement.DeleteAccountMovementByIdAsync(movementId, connection, transaction);
                if (!deleted)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                if (isPlannedRentDebit)
                    await _rentalAmountHistoryService.RemoveOrphanedPlannedHistoriesTransactionAsync(rentalId, connection, transaction);
                await _rentalAmountHistoryService.NormalizeRentalAmountHistoryTransactionAsync(rentalId, connection, transaction);
                await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rentalId, connection, transaction);
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<AccountMovement> CreateManualMovementAsync(CreateAccountMovementDTO dto)
        {
            if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than 0.");
            if (string.IsNullOrWhiteSpace(dto.Concept)) throw new ArgumentException("Concept is required.");

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                // 1. Buscar el rentalId activo del cliente
                var rental = await _daoRental.GetActiveRentalByClientIdTransactionAsync(dto.ClientId, connection, transaction);
                if (rental == null) throw new InvalidOperationException("No se encontró un alquiler activo para este cliente.");

                // ACÁ ESTÁ TU DATETIME: Carga la fecha y hora exacta que mandó Angular, o la actual si viene nula.
                DateTime movDate = dto.Date ?? DateTime.Now;

                // 2. Crear la entidad AccountMovement (Libro Diario)
                var movement = new AccountMovement
                {
                    RentalId = rental.Id,
                    MovementDate = movDate,
                    MovementType = dto.MovementType.ToUpper(), // "DEBITO" o "CREDITO"
                    Concept = dto.Concept,
                    Amount = dto.Amount,
                    PaymentId = null
                };

                // 3. Guardar el movimiento físico
                await _daoAccountMovement.CreateAccountMovementTransactionAsync(movement, connection, transaction);

                // 4. LIMPIEZA DE MORA SI EL SALDO GLOBAL ES 0 (O a favor)
                decimal newGlobalBalance = await _daoRental.GetBalanceByRentalIdTransactionAsync(rental.Id, connection, transaction);
                if (newGlobalBalance <= 0)
                {
                    await _daoRental.ResetUnpaidMonthsTransactionAsync(rental.Id, connection, transaction);
                }

                // 5. LA MAGIA: El Rebuild lee el movimiento nuevo y reconstruye todo el Excel solo.
                await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rental.Id, connection, transaction);
                
                await transaction.CommitAsync();
                return movement;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> IsDebitAlreadyCreatedAsync(int rentalId, string concept, SqlConnection conn, SqlTransaction trans)
        {
            return await _daoAccountMovement.IsDebitAlreadyCreatedAsync(rentalId, concept, conn, trans);
        }

        public async Task<PaymentPlanningContextDto> GetPaymentPlanningContextAsync(int clientId, int months)
        {
            ValidatePaymentPlan(clientId, months);

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            var rental = await _daoRental.GetActiveRentalByClientIdTransactionAsync(clientId, connection, transaction)
                ?? throw new InvalidOperationException("No se encontró un alquiler activo para este cliente.");

            var context = await BuildPlanningContextAsync(clientId, rental, months, connection, transaction);
            await transaction.CommitAsync();
            return context;
        }

        public async Task<PlannedPaymentResultDto> PlanClientPaymentAsync(PlanClientPaymentDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            ValidatePaymentPlan(dto.ClientId, dto.Months);

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                await AcquirePaymentPlanningLockAsync(dto.ClientId, connection, transaction);

                var rental = await _daoRental.GetActiveRentalByClientIdTransactionAsync(dto.ClientId, connection, transaction)
                    ?? throw new InvalidOperationException("No se encontró un alquiler activo para este cliente.");
                var context = await BuildPlanningContextAsync(dto.ClientId, rental, dto.Months, connection, transaction);

                var suppliedIncreases = dto.AppliedIncreases ?? [];
                if (context.IsPriceLocked && suppliedIncreases.Count > 0)
                    throw new InvalidOperationException("Un período de seis meses o más mantiene el abono sin aumentos.");

                if (!context.IsPriceLocked)
                    ValidateAppliedIncreases(context.Increases, suppliedIncreases);

                var appliedByMonth = suppliedIncreases.ToDictionary(x => x.Year * 100 + x.Month);
                var currentRent = context.BaseRent;
                var result = new PlannedPaymentResultDto
                {
                    StartDate = context.StartDate,
                    EndDate = context.EndDate,
                    IsPriceLocked = context.IsPriceLocked
                };

                for (var index = 0; index < dto.Months; index++)
                {
                    var monthDate = context.StartDate.AddMonths(index);
                    var monthKey = monthDate.Year * 100 + monthDate.Month;

                    if (!context.IsPriceLocked && appliedByMonth.TryGetValue(monthKey, out var increase))
                    {
                        var latestHistory = await _rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(rental.Id, connection, transaction)
                            ?? throw new InvalidOperationException("El alquiler no posee un historial de abono válido.");
                        await _rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(
                            latestHistory.Id, rental.Id, increase.NewRentAmount, monthDate, connection, transaction);
                        currentRent = increase.NewRentAmount;
                    }

                    var isHalfPromotion = context.HasSixMonthPromotion && dto.ChargeHalfSixthMonth && index == 5;
                    var amount = isHalfPromotion ? decimal.Round(currentRent / 2m, 2, MidpointRounding.AwayFromZero) : currentRent;
                    var concept = BuildPlannedRentConcept(monthDate);

                    if (await _daoAccountMovement.IsDebitAlreadyCreatedAsync(rental.Id, BuildRentConcept(monthDate), connection, transaction))
                        throw new InvalidOperationException($"Ya existe el débito de {FormatMonthLabel(monthDate)}. Actualizá los datos e intentá nuevamente.");

                    await _daoAccountMovement.CreateAccountMovementTransactionAsync(new AccountMovement
                    {
                        RentalId = rental.Id,
                        MovementDate = monthDate,
                        MovementType = "DEBITO",
                        Concept = concept,
                        Amount = amount,
                        PaymentId = null
                    }, connection, transaction);

                    result.Months.Add(new PaymentPlanningMonthDto
                    {
                        Year = monthDate.Year,
                        Month = monthDate.Month,
                        Label = FormatMonthLabel(monthDate),
                        Amount = amount,
                        IsHalfPromotion = isHalfPromotion
                    });
                }

                if (context.IsPriceLocked)
                {
                    // El precio queda congelado durante todos los débitos generados.
                    // El próximo aumento debe quedar exactamente en el mes siguiente
                    // al último débito del período planificado.
                    var nextIncreaseAfterPlan = context.EndDate.AddMonths(1);
                    await _daoRental.UpdatePriceLockEndDateTransactionAsync(rental.Id, nextIncreaseAfterPlan, connection, transaction);
                    await _daoRental.UpdateIncreaseAnchorDateTransactionAsync(rental.Id, nextIncreaseAfterPlan, connection, transaction);
                }
                else if (context.Increases.Count > 0 && rental.IncreaseAnchorDate.HasValue)
                {
                    var step = Math.Max(1, context.IncreaseFrequencyMonths - 1);
                    var nextAnchor = new DateTime(rental.IncreaseAnchorDate.Value.Year, rental.IncreaseAnchorDate.Value.Month, 1);
                    while (nextAnchor < context.StartDate)
                        nextAnchor = nextAnchor.AddMonths(step);
                    foreach (var _ in context.Increases)
                        nextAnchor = nextAnchor.AddMonths(step);
                    await _daoRental.UpdateIncreaseAnchorDateTransactionAsync(rental.Id, nextAnchor, connection, transaction);
                }

                // La planificación puede crear o actualizar tramos futuros. Antes
                // de reconstruir saldos dejamos la cadena de abonos sin duplicados
                // y con end_date alineado al siguiente start_date.
                await _rentalAmountHistoryService.NormalizeRentalAmountHistoryTransactionAsync(rental.Id, connection, transaction);
                await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rental.Id, connection, transaction);
                await transaction.CommitAsync();

                result.CreatedDebits = result.Months.Count;
                result.TotalAmount = result.Months.Sum(x => x.Amount);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<PaymentPlanningContextDto> BuildPlanningContextAsync(
            int clientId,
            Rental rental,
            int months,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            var startDate = await GetNextRentDebitMonthAsync(rental.Id, rental.StartDate, connection, transaction);
            var baseRent = await GetRentAmountForMonthAsync(rental.Id, startDate, connection, transaction);
            if (baseRent <= 0)
                throw new InvalidOperationException("El cliente no posee un abono válido para el período a planificar.");

            var frequency = await GetIncreaseFrequencyAsync(clientId, connection, transaction);
            var hasSixMonthPromotion = await GetSixMonthPromotionAsync(clientId, connection, transaction);
            var context = new PaymentPlanningContextDto
            {
                ClientId = clientId,
                Months = months,
                StartDate = startDate,
                EndDate = startDate.AddMonths(months - 1),
                BaseRent = baseRent,
                IncreaseFrequencyMonths = frequency,
                IncreaseAnchorDate = rental.IncreaseAnchorDate,
                HasSixMonthPromotion = hasSixMonthPromotion,
                IsPriceLocked = months >= 6
            };

            if (context.IsPriceLocked || !rental.IncreaseAnchorDate.HasValue)
                return context;

            var step = Math.Max(1, frequency - 1);
            var anchor = new DateTime(rental.IncreaseAnchorDate.Value.Year, rental.IncreaseAnchorDate.Value.Month, 1);
            while (anchor < startDate)
                anchor = anchor.AddMonths(step);

            for (var index = 0; index < months; index++)
            {
                var monthDate = startDate.AddMonths(index);
                if (monthDate >= anchor)
                {
                    context.Increases.Add(new PaymentPlanningIncreaseDto
                    {
                        Year = monthDate.Year,
                        Month = monthDate.Month,
                        Label = FormatMonthLabel(monthDate)
                    });
                    anchor = anchor.AddMonths(step);
                }
            }

            return context;
        }

        private static void ValidatePaymentPlan(int clientId, int months)
        {
            if (clientId <= 0) throw new ArgumentException("El cliente es inválido.");
            if (months < 1 || months > 24) throw new ArgumentException("La cantidad de meses debe estar entre 1 y 24.");
        }

        private static void ValidateAppliedIncreases(
            IReadOnlyCollection<PaymentPlanningIncreaseDto> expected,
            IReadOnlyCollection<PlannedPaymentIncreaseDto> supplied)
        {
            var expectedKeys = expected.Select(x => x.Year * 100 + x.Month).OrderBy(x => x).ToArray();
            var suppliedKeys = supplied.Select(x => x.Year * 100 + x.Month).OrderBy(x => x).ToArray();
            if (!expectedKeys.SequenceEqual(suppliedKeys))
                throw new InvalidOperationException("Los aumentos informados no coinciden con el período planificado. Actualizá la vista previa.");
            if (supplied.Any(x => x.Percentage < 0 || x.NewRentAmount <= 0))
                throw new InvalidOperationException("Los montos y porcentajes de aumento deben ser válidos.");
        }

        private static async Task AcquirePaymentPlanningLockAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            using var command = new SqlCommand("sp_getapplock", connection, transaction) { CommandType = CommandType.StoredProcedure };
            // Debe coincidir con PaymentService para serializar planificación y pago real.
            command.Parameters.AddWithValue("@Resource", $"payment-client:{clientId}");
            command.Parameters.AddWithValue("@LockMode", "Exclusive");
            command.Parameters.AddWithValue("@LockOwner", "Transaction");
            command.Parameters.AddWithValue("@LockTimeout", 15000);
            var returnValue = command.Parameters.Add("@RETURN_VALUE", SqlDbType.Int);
            returnValue.Direction = ParameterDirection.ReturnValue;
            await command.ExecuteNonQueryAsync();
            if (Convert.ToInt32(returnValue.Value) < 0)
                throw new InvalidOperationException("No se pudo bloquear la cuenta del cliente para planificar el pago. Intentá nuevamente.");
        }

        private static async Task<int> GetIncreaseFrequencyAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT increase_frequency_months FROM clients WHERE client_id = @client_id";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId });
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? 4 : Math.Max(1, Convert.ToInt32(value));
        }

        private static async Task<bool> GetSixMonthPromotionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT is_six_month_promotion FROM clients WHERE client_id = @client_id";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId });
            var value = await command.ExecuteScalarAsync();
            return value != null && value != DBNull.Value && Convert.ToBoolean(value);
        }

        private static async Task<DateTime> GetNextRentDebitMonthAsync(int rentalId, DateTime rentalStart, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                SELECT MAX(DATEFROMPARTS(YEAR(movement_date), MONTH(movement_date), 1))
                FROM account_movements
                WHERE rental_id = @rental_id
                  AND movement_type = 'DEBITO'
                  AND concept LIKE 'Alquiler %';";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            var value = await command.ExecuteScalarAsync();
            var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var firstPossibleMonth = new DateTime(rentalStart.Year, rentalStart.Month, 1) > currentMonth
                ? new DateTime(rentalStart.Year, rentalStart.Month, 1)
                : currentMonth;
            if (value == null || value == DBNull.Value) return firstPossibleMonth;
            var nextMonth = Convert.ToDateTime(value).AddMonths(1);
            return nextMonth > firstPossibleMonth ? nextMonth : firstPossibleMonth;
        }

        private static async Task<decimal> GetRentAmountForMonthAsync(int rentalId, DateTime month, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                SELECT TOP 1 amount
                FROM rental_amount_history
                WHERE rental_id = @rental_id
                  AND start_date < DATEADD(month, 1, @month)
                  AND (end_date IS NULL OR end_date >= @month)
                ORDER BY start_date DESC, rental_amount_history_id DESC;";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            command.Parameters.Add(new SqlParameter("@month", SqlDbType.Date) { Value = month });
            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        private static string BuildRentConcept(DateTime month)
        {
            var culture = new CultureInfo("es-AR");
            var monthName = culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(month.Month));
            return $"Alquiler {monthName} {month.Year}";
        }

        private static string BuildPlannedRentConcept(DateTime month)
        {
            return $"{BuildRentConcept(month)} (Planificado)";
        }

        private static string FormatMonthLabel(DateTime month)
        {
            var culture = new CultureInfo("es-AR");
            return $"{culture.DateTimeFormat.GetMonthName(month.Month)} {month.Year}";
        }
    }
}
