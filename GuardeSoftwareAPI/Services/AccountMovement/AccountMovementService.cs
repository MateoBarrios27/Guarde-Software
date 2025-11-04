using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Dtos.AccountMovement;

namespace GuardeSoftwareAPI.Services.accountMovement {

    public class AccountMovementService : IAccountMovementService
    {
        private readonly DaoAccountMovement _daoAccountMovement;
        private readonly DaoRental _daoRental;
        private readonly ILogger<IAccountMovementService> _logger;
        private readonly AccessDB accessDB;

        public AccountMovementService(AccessDB _accessDB, ILogger<AccountMovementService> logger)
        {
            _daoAccountMovement = new DaoAccountMovement(_accessDB);
            _daoRental = new DaoRental(_accessDB);
            _logger = logger;
            accessDB = _accessDB;
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
            var activeRentalIds = await _daoRental.GetActiveRentalsIdsAsync();
            _logger.LogInformation($"Se encontraron {activeRentalIds.Count} alquileres activos para procesar.");

            int skippedCount = 0;
            int processedCount = 0;

            // Procesamos cada rental individualmente
            foreach (var rentalId in activeRentalIds)
            {
                // Abrimos una conexión POR CADA rental para aislar fallos
                using (var connection = accessDB.GetConnectionClose())
                {
                    try
                    {
                        await connection.OpenAsync();

                        // 1. Verificar si ya existe débito este mes (usando la conexión)
                        bool debitExists = await _daoAccountMovement.CheckIfDebitExistsForCurrentMonthAsync(rentalId, connection);
                        if (debitExists)
                        {
                            _logger.LogDebug($"Débito ya existe para Rental ID {rentalId} este mes. Omitiendo.");
                            continue;
                        }

                        // 2. Obtener balance actual y monto de alquiler (usando la conexión)
                        decimal currentBalance = await _daoRental.GetBalanceByRentalIdAsync(rentalId, connection);
                        decimal currentAmount = await _daoRental.GetCurrentRentAmountAsync(rentalId, connection);

                        _logger.LogDebug($"Rental ID {rentalId}: Balance actual={currentBalance:C}, Monto alquiler={currentAmount:C}");

                        if (currentAmount <= 0)
                        {
                            _logger.LogWarning($"El monto de alquiler para Rental ID {rentalId} es cero o negativo ({currentAmount:C}). Omitiendo débito.");
                            continue;
                        }

                        // 3. Decidir si aplicar débito (Lógica de Crédito)
                        // Si el balance + el débito a aplicar sigue siendo <= 0, significa que tiene crédito suficiente
                        if (currentBalance + currentAmount <= 0)
                        {
                            _logger.LogInformation($"Rental ID {rentalId} tiene suficiente crédito ({currentBalance:C}) para cubrir el débito de {currentAmount:C}. Omitiendo débito este mes.");
                            skippedCount++;
                            continue; // Saltar al siguiente rental
                        }

                        // 4. Generar concepto
                        var culture = new CultureInfo("es-AR");
                        string monthName = culture.DateTimeFormat.GetMonthName(DateTime.Now.Month);
                        string concept = $"Alquiler {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName)} {DateTime.Now.Year}";

                        // 5. Crear objeto débito
                        var debitMovement = new AccountMovement
                        {
                            RentalId = rentalId,
                            MovementDate = DateTime.Now,
                            MovementType = "DEBITO",
                            Amount = currentAmount,
                            Concept = concept,
                            PaymentId = null
                        };

                        // 6. Crear débito en BD (usando la conexión)
                        await _daoAccountMovement.CreateDebitAsync(debitMovement, connection);
                        _logger.LogInformation($"Débito de {currentAmount:C} creado para Rental ID {rentalId}. Concepto: {concept}");
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error procesando Rental ID {rentalId} en ApplyMonthlyDebits: {ex.Message}");
                        // Continuar con el siguiente
                    }
                    // La conexión se cierra automáticamente por el 'using'
                }
            }
            _logger.LogInformation($"--- Job Aplicador de Débitos finalizado. Procesados: {processedCount}, Omitidos por crédito: {skippedCount} ---");
        }

        /// <summary>
        /// Obtiene los movimientos de cuenta usando el ID del cliente (buscando su rentalID primero).
        /// </summary>
        public async Task<List<AccountMovement>> GetAccountMovementListByClientIdAsync(int clientId)
        {
            // 1. Encontrar el rentalId activo para este cliente
            // Usamos el método de DaoRental que ya existe
            DataTable rentalTable = await _daoRental.GetRentalsByClientId(clientId);

            if (rentalTable.Rows.Count == 0)
            {
                _logger.LogWarning($"No se encontró un alquiler (rental) activo para el cliente ID {clientId}.");
                return new List<AccountMovement>(); // Devolver lista vacía
            }

            // Asumimos que un cliente solo tiene un rental activo (o tomamos el primero)
            int rentalId = Convert.ToInt32(rentalTable.Rows[0]["rental_id"]);

            // 2. Reutilizar la lógica existente para obtener movimientos por rentalId
            return await GetAccountMovementListByRentalId(rentalId);
        }

        /// <summary>
        /// Elimina un movimiento de cuenta, solo si no está atado a un pago.
        /// </summary>
        public async Task<bool> DeleteAccountMovementAsync(int movementId)
        {
            // 1. Obtener el movimiento para verificar si está atado a un pago
            DataTable movTable = await _daoAccountMovement.GetAccountMovById(movementId);
            if (movTable.Rows.Count == 0)
            {
                _logger.LogWarning($"No se encontró el movimiento ID {movementId} para eliminar.");
                return false; // No encontrado
            }

            DataRow row = movTable.Rows[0];
            int? paymentId = row["payment_id"] != DBNull.Value ? (int)row["payment_id"] : null;

            // 2. Regla de Negocio: No permitir borrar movimientos de "CREDITO" asociados a un pago
            if (paymentId.HasValue && paymentId > 0)
            {
                _logger.LogError($"Intento de eliminar el movimiento ID {movementId}, pero está asociado al pago ID {paymentId}.");
                throw new InvalidOperationException("No se puede eliminar un movimiento que está asociado a un pago registrado. Primero debe anular el pago.");
            }

            // 3. Si es seguro, proceder a eliminar
            _logger.LogInformation($"Eliminando movimiento ID {movementId} (no asociado a pago).");
            return await _daoAccountMovement.DeleteAccountMovementByIdAsync(movementId);
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
                if (rental == null)
                {
                    throw new InvalidOperationException("No se encontró un alquiler activo para este cliente.");
                }

                // 2. Crear la entidad AccountMovement
                var movement = new AccountMovement
                {
                    RentalId = rental.Id,
                    MovementDate = DateTime.Now,
                    MovementType = dto.MovementType.ToUpper(), // "DEBITO" o "CREDITO"
                    Concept = dto.Concept,
                    Amount = dto.Amount,
                    PaymentId = null // Es un movimiento manual
                };

                // 3. Guardar el movimiento en la BD
                await _daoAccountMovement.CreateAccountMovementTransactionAsync(movement, connection, transaction);

                // 4. REVISAR BALANCE Y MORA (Lógica clave)
                // Si fue un CRÉDITO, chequear si saldó la deuda
                if (movement.MovementType == "CREDITO")
                {
                    decimal newBalance = await _daoRental.GetBalanceByRentalIdTransactionAsync(rental.Id, connection, transaction);
                    _logger.LogInformation($"Movimiento manual de {dto.Amount:C} registrado para Rental ID {rental.Id}. Nuevo balance: {newBalance:C}");

                    // Si el balance es 0 o negativo (a favor), reseteamos la mora
                    if (newBalance <= 0)
                    {
                        await _daoRental.ResetUnpaidMonthsTransactionAsync(rental.Id, connection, transaction);
                        _logger.LogInformation($"Balance saldado para Rental ID {rental.Id}. Contador de meses impagos reseteado a 0.");
                    }
                }

                await transaction.CommitAsync();
                return movement; // Devuelve la entidad creada
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error en CreateManualMovementAsync. Transacción revertida.");
                throw;
            }
        }
    }
}