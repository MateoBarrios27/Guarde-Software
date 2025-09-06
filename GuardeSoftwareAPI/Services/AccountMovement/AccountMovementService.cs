using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace GuardeSoftwareAPI.Services.accountMovement {

    public class AccountMovementService : IAccountMovementService
    {
        private readonly DaoAccountMovement _daoAccountMovement;
        private readonly DaoRental _daoRental;
        private readonly ILogger<IAccountMovementService> _logger;

        public AccountMovementService(AccessDB accessDB, ILogger<AccountMovementService> logger)
        {
            _daoAccountMovement = new DaoAccountMovement(accessDB);
            _daoRental = new DaoRental(accessDB);
            _logger = logger;
        }

        public List<AccountMovement> GetAccountMovementList()
        {

            DataTable AccountsTable = _daoAccountMovement.GetAccountMovement();
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

        public List<AccountMovement> GetAccountMovementListByRentalId(int id)
        {
            DataTable AccountsTable = _daoAccountMovement.GetAccountMovByRentalId(id);
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

        public bool CreateAccountMovement(AccountMovement accountMovement)
        {

            if (accountMovement == null)
                throw new ArgumentNullException(nameof(accountMovement));

            if (accountMovement.RentalId <= 0)
                throw new ArgumentException("invalid rental ID.");

            if (string.IsNullOrWhiteSpace(accountMovement.MovementType))
                throw new ArgumentException("MovementType required.");

            if (accountMovement.Amount <= 0)
                throw new ArgumentException("Amount must to be > 0");

            if (_daoAccountMovement.CreateAccountMovement(accountMovement)) return true;
            else return false;
        }

        public async Task ApplyMonthlyDebitsAsync()
        {
            _logger.LogInformation("Getting active rents...");
            var activeRentalIds = await _daoRental.GetActiveRentalsIdsAsync();
            _logger.LogInformation($"{activeRentalIds.Count} active rentals funded.");

            foreach (var rentalId in activeRentalIds)
            {
                try
                {
                    // 1. Verifies if the debit already exists for this rental this month
                    bool debitExists = await _daoAccountMovement.CheckIfDebitExistsForCurrentMonthAsync(rentalId);
                    if (debitExists)
                    {
                        _logger.LogWarning($"The rental ID {rentalId} already have a debit. Omitting.");
                        continue;
                    }

                    // 2. Get the current rent amount
                    decimal currentAmount = await _daoRental.GetCurrentRentAmountAsync(rentalId);
                    if (currentAmount <= 0)
                    {
                        _logger.LogWarning($"The amount for rental ID {rentalId} is cero or negative ({currentAmount}). Omitting.");
                        continue;
                    }

                    // 3. Generate the concept string
                    var culture = new CultureInfo("es-AR");
                    string monthName = culture.DateTimeFormat.GetMonthName(DateTime.Now.Month);
                    string concept = $"Alquiler {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName)} {DateTime.Now.Year}";

                    // 4. Create the debit movement object
                    var debitMovement = new AccountMovement
                    {
                        RentalId = rentalId,
                        MovementDate = DateTime.Now,
                        MovementType = "DEBITO",
                        Amount = currentAmount,
                        Concept = concept
                    };

                    // 5. Create the debit in the database
                    await _daoAccountMovement.CreateDebitAsync(debitMovement);
                    _logger.LogInformation($"Debit created for rental ID {rentalId}, amount: {currentAmount}, concept: {concept}");

                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, $"Error processing rental ID {rentalId}: {ex.Message}");

                }
                // Continue with the next rental ID even if there was an error
            }
        }
        

    }
}