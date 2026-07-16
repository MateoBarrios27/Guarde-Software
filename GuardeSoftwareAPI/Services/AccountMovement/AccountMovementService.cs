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

namespace GuardeSoftwareAPI.Services.accountMovement {

    public class AccountMovementService : IAccountMovementService
    {
        private readonly DaoAccountMovement _daoAccountMovement;
        private readonly DaoRental _daoRental;
        private readonly ILogger<IAccountMovementService> _logger;
        private readonly AccessDB accessDB;
        private readonly IClientMonthBalanceService _clientMonthBalanceService;
        private readonly IServiceProvider _serviceProvider;

        public AccountMovementService(AccessDB _accessDB, ILogger<AccountMovementService> logger, IClientMonthBalanceService clientMonthBalanceService, IServiceProvider serviceProvider)
        {
            _daoAccountMovement = new DaoAccountMovement(_accessDB);
            _daoRental = new DaoRental(_accessDB);
            _logger = logger;
            accessDB = _accessDB;
            _clientMonthBalanceService = clientMonthBalanceService;
            _serviceProvider = serviceProvider;
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
            
            // ¡NUEVO! Rescatamos a qué alquiler pertenece para recalcular después
            int rentalId = Convert.ToInt32(row["rental_id"]);

            // 2. Si es un movimiento asociado a un pago formal, delegamos a IPaymentService para eliminar el pago completo con su cascada sin restricciones
            if (paymentId.HasValue && paymentId > 0)
            {
                _logger.LogInformation($"El movimiento ID {movementId} está asociado al pago ID {paymentId}. Eliminando el pago completo en cascada desde IPaymentService.");
                using var scope = _serviceProvider.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                return await paymentService.DeletePaymentAsync(movementId);
            }

            // 3. Borramos el movimiento
            _logger.LogInformation($"Eliminando movimiento ID {movementId}.");
            bool deleted = await _daoAccountMovement.DeleteAccountMovementByIdAsync(movementId);

            // 4. ¡LA CASCADA CONTABLE!
            if (deleted)
            {
                _logger.LogInformation($"Reconstruyendo la cuenta corriente del alquiler ID {rentalId} tras la eliminación del movimiento.");
                
                // Al ejecutarse esto, la fila de ClientMonthBalance se recalculará sin el movimiento que acabamos de borrar
                await _clientMonthBalanceService.RebuildForRentalAsync(rentalId);
            }

            return deleted;
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
    }
}
