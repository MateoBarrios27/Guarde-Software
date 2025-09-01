using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Collections.Generic;
using System.Data;

namespace GuardeSoftwareAPI.Services.accountMovement { 

	public class AccountMovementService : IAccountMovementService
    {
		private readonly DaoAccountMovement _daoAccountMovement;

		public AccountMovementService(AccessDB accessDB)
		{
			_daoAccountMovement = new DaoAccountMovement(accessDB);
		}

		public List<AccountMovement> GetAccountMovementList() {

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

        public bool CreateAccountMovement(AccountMovement accountMovement) {

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
    }
}