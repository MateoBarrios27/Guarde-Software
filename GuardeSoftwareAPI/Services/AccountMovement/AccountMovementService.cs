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

namespace GuardeSoftwareAPI.Services.accountMovement {

    public class AccountMovementService : IAccountMovementService
    {
        private readonly DaoAccountMovement _daoAccountMovement;
        private readonly DaoRental _daoRental;
        private readonly ILogger<IAccountMovementService> _logger;
        private readonly AccessDB accessDB;
        private readonly IClientMonthBalanceService _clientMonthBalanceService;

        public AccountMovementService(AccessDB _accessDB, ILogger<AccountMovementService> logger, IClientMonthBalanceService clientMonthBalanceService)
        {
            _daoAccountMovement = new DaoAccountMovement(_accessDB);
            _daoRental = new DaoRental(_accessDB);
            _logger = logger;
            accessDB = _accessDB;
            _clientMonthBalanceService = clientMonthBalanceService;
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
                        decimal currentBalance = await _daoRental.GetBalanceByRentalIdAsync(rentalId, connection);
                        decimal currentAmount = await _daoRental.GetCurrentRentAmountAsync(rentalId, connection, transaction);

                        _logger.LogDebug($"Rental ID {rentalId}: Balance actual={currentBalance:C}, Monto alquiler={currentAmount:C}");

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

        public async Task<bool> DeleteAccountMovementAsync(int movementId)
        {
            // 1. Obtains the movement to check if it's associated with a payment
            DataTable movTable = await _daoAccountMovement.GetAccountMovById(movementId);
            if (movTable.Rows.Count == 0)
            {
                _logger.LogWarning($"No se encontró el movimiento ID {movementId} para eliminar.");
                return false; // No encontrado
            }

            DataRow row = movTable.Rows[0];
            int? paymentId = row["payment_id"] != DBNull.Value ? (int)row["payment_id"] : null;

            // 2. Rule: Isn't allowed to delete a movement if it's associated with a payment
            if (paymentId.HasValue && paymentId > 0)
            {
                _logger.LogError($"Intento de eliminar el movimiento ID {movementId}, pero está asociado al pago ID {paymentId}.");
                throw new InvalidOperationException("No se puede eliminar un movimiento que está asociado a un pago registrado. Vé al componente de finanzas para eliminar el pago.");
            }

            // 3. If it's not associated with a payment, proceed to delete
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
                if (rental == null) throw new InvalidOperationException("No se encontró un alquiler activo para este cliente.");

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

                await _daoAccountMovement.CreateAccountMovementTransactionAsync(movement, connection, transaction);

                // ==============================================================================
                // 3. IMPACTO EN EL ESTADO DE CUENTA MENSUAL (EXCEL)
                // ==============================================================================
                
                DateTime currentRealMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                string currentMonthStr = currentRealMonth.ToString("MM/yyyy");

                if (movement.MovementType == "CREDITO")
                {
                    // --- LÓGICA DE CASCADA PARA CRÉDITOS ---
                    decimal moneyInHand = dto.Amount;
                    
                    var existingMonths = new List<ClientMonthBalance>();
                    string selectQuery = "SELECT id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment FROM client_month_balances WHERE rental_id = @rental_id ORDER BY id ASC";
                    using (var cmdSelect = new SqlCommand(selectQuery, connection, transaction))
                    {
                        cmdSelect.Parameters.AddWithValue("@rental_id", rental.Id);
                        using (var reader = await cmdSelect.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                existingMonths.Add(new ClientMonthBalance {
                                    Id = reader.GetInt32(0), MonthYear = reader.GetString(1), PreviousBalance = reader.GetDecimal(2),
                                    Interests = reader.GetDecimal(3), MonthlyDebits = reader.GetDecimal(4), Balance = reader.GetDecimal(5),
                                    Paid = reader.GetDecimal(6), AdvancedPayment = reader.GetDecimal(7)
                                });
                            }
                        }
                    }

                    if (existingMonths.Count > 0)
                    {
                        decimal rolledOverDebt = 0;

                        // A. Llenamos deudas existentes
                        for (int i = 0; i < existingMonths.Count; i++)
                        {
                            var month = existingMonths[i];
                            if (i > 0) 
                            {
                                month.PreviousBalance = rolledOverDebt;
                                month.Balance = month.PreviousBalance + month.Interests + month.MonthlyDebits;
                                string updBal = "UPDATE client_month_balances SET previous_balance = @pb, balance = @b WHERE id = @id";
                                using var cmdBal = new SqlCommand(updBal, connection, transaction);
                                cmdBal.Parameters.AddWithValue("@pb", month.PreviousBalance);
                                cmdBal.Parameters.AddWithValue("@b", month.Balance);
                                cmdBal.Parameters.AddWithValue("@id", month.Id);
                                await cmdBal.ExecuteNonQueryAsync();
                            }

                            decimal owes = month.Balance - (month.Paid + month.AdvancedPayment);
                            if (owes > 0 && moneyInHand > 0)
                            {
                                decimal applied = Math.Min(moneyInHand, owes);
                                DateTime rowMonth = DateTime.ParseExact(month.MonthYear, "MM/yyyy", null);
                                string colToUpdate = (rowMonth > currentRealMonth) ? "advanced_payment" : "paid";
                                
                                string updPaid = $"UPDATE client_month_balances SET {colToUpdate} = {colToUpdate} + @app WHERE id = @id";
                                using var cmdPaid = new SqlCommand(updPaid, connection, transaction);
                                cmdPaid.Parameters.AddWithValue("@app", applied);
                                cmdPaid.Parameters.AddWithValue("@id", month.Id);
                                await cmdPaid.ExecuteNonQueryAsync();

                                if (colToUpdate == "paid") month.Paid += applied; else month.AdvancedPayment += applied;
                                moneyInHand -= applied;
                            }
                            rolledOverDebt = month.Balance - (month.Paid + month.AdvancedPayment);
                        }

                        // B. Generamos futuro si sobra plata (Crédito a favor)
                        string lastMonthStr = existingMonths.Last().MonthYear;
                        DateTime lastGeneratedDate = DateTime.ParseExact(lastMonthStr, "MM/yyyy", null);
                        decimal lastMonthDebt = rolledOverDebt;
                        
                        // A diferencia del pago normal, un crédito manual solo genera filas futuras si REALMENTE sobra plata
                        while (moneyInHand > 0) 
                        {
                            lastGeneratedDate = lastGeneratedDate.AddMonths(1);
                            string newMonthStr = lastGeneratedDate.ToString("MM/yyyy");

                            decimal prevBal = lastMonthDebt > 0 ? lastMonthDebt : 0m;
                            decimal rentAmount = (decimal)rental.CurrentAmount;
                            decimal bucketSize = Math.Max(rentAmount, prevBal + rentAmount);
                            decimal applied = Math.Min(moneyInHand, bucketSize);

                            string insQuery = @"INSERT INTO client_month_balances (rental_id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment)
                                        VALUES (@rid, @my, @pb, 0, @md, @pb + @md, 0, @adv)";
                            using (var cmdIns = new SqlCommand(insQuery, connection, transaction))
                            {
                                cmdIns.Parameters.AddWithValue("@rid", rental.Id);
                                cmdIns.Parameters.AddWithValue("@my", newMonthStr);
                                cmdIns.Parameters.AddWithValue("@pb", prevBal);
                                cmdIns.Parameters.AddWithValue("@md", rentAmount);
                                cmdIns.Parameters.AddWithValue("@adv", applied);
                                await cmdIns.ExecuteNonQueryAsync();
                            }
                            moneyInHand -= applied;
                            lastMonthDebt = (prevBal + rentAmount) - applied;
                        }
                    }
                }
                else if (movement.MovementType == "DEBITO")
                {
                    // --- LÓGICA INTELIGENTE PARA DÉBITOS ---
                    DateTime movMonth = new DateTime(movDate.Year, movDate.Month, 1);
                    
                    // 1. Buscamos si existe la tabla del mes actual (HOY)
                    string checkQuery = "SELECT id FROM client_month_balances WHERE rental_id = @rid AND month_year = @my";
                    int? currentMonthId = null;
                    using (var cmdCheck = new SqlCommand(checkQuery, connection, transaction))
                    {
                        cmdCheck.Parameters.AddWithValue("@rid", rental.Id);
                        cmdCheck.Parameters.AddWithValue("@my", currentMonthStr);
                        var result = await cmdCheck.ExecuteScalarAsync();
                        if (result != null) currentMonthId = Convert.ToInt32(result);
                    }

                    // 2. Si no existe, la creamos de urgencia para alojar la deuda
                    if (currentMonthId == null)
                    {
                        string insMonth = @"INSERT INTO client_month_balances (rental_id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment)
                                    OUTPUT INSERTED.id
                                    VALUES (@rid, @my, 0, 0, 0, 0, 0, 0)";
                        using (var cmdInsMonth = new SqlCommand(insMonth, connection, transaction))
                        {
                            cmdInsMonth.Parameters.AddWithValue("@rid", rental.Id);
                            cmdInsMonth.Parameters.AddWithValue("@my", currentMonthStr);
                            currentMonthId = (int)await cmdInsMonth.ExecuteScalarAsync();
                        }
                    }

                    // 3. Aplicamos el Débito donde corresponde
                    if (movMonth >= currentRealMonth)
                    {
                        // El débito es de ESTE mes o futuro -> Suma a Monthly Debits (Abono)
                        string updDebit = "UPDATE client_month_balances SET monthly_debits = monthly_debits + @amt, balance = balance + @amt WHERE id = @id";
                        using var cmdUpdDebit = new SqlCommand(updDebit, connection, transaction);
                        cmdUpdDebit.Parameters.AddWithValue("@amt", dto.Amount);
                        cmdUpdDebit.Parameters.AddWithValue("@id", currentMonthId.Value);
                        await cmdUpdDebit.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // El débito es de un mes VIEJO -> Suma a Previous Balance (Saldo Anterior) del mes actual
                        string updOld = "UPDATE client_month_balances SET previous_balance = previous_balance + @amt, balance = balance + @amt WHERE id = @id";
                        using var cmdUpdOld = new SqlCommand(updOld, connection, transaction);
                        cmdUpdOld.Parameters.AddWithValue("@amt", dto.Amount);
                        cmdUpdOld.Parameters.AddWithValue("@id", currentMonthId.Value);
                        await cmdUpdOld.ExecuteNonQueryAsync();
                    }
                }

                // 4. LIMPIEZA DE MORA SI EL SALDO GLOBAL ES 0
                decimal newGlobalBalance = await _daoRental.GetBalanceByRentalIdTransactionAsync(rental.Id, connection, transaction);
                if (newGlobalBalance <= 0)
                {
                    await _daoRental.ResetUnpaidMonthsTransactionAsync(rental.Id, connection, transaction);
                }

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
