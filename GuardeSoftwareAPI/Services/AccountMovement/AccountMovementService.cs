using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

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

            if (accountMovement.Amount <= 0)
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
        

    }
}