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

                        // 1. Verificar si ya existe un débito con este CONCEPTO (Corrección clave)
                        bool debitExists = await _daoAccountMovement.CheckIfDebitExistsByConceptAsync(rentalId, targetConceptBase, connection);
                        
                        if (debitExists)
                        {
                            _logger.LogDebug($"Débito omitido para Rental ID {rentalId}: Ya existe un movimiento con concepto '{targetConceptBase}'.");
                            duplicateCount++;
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
                        await _daoAccountMovement.CreateDebitAsync(debitMovement, connection);
                        
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