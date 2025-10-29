using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.rental
{

	public class RentalService : IRentalService
	{
		readonly DaoRental _daoRental;
		public RentalService(AccessDB accessDB)
		{
			_daoRental = new DaoRental(accessDB);
		}

		public async Task<List<Rental>> GetRentalsList()
		{
			DataTable rentalTable = await _daoRental.GetRentals();
			List<Rental> rentals = new List<Rental>();

			if (rentalTable.Rows.Count == 0) throw new ArgumentException("No rentals found.");

			foreach (DataRow row in rentalTable.Rows)
			{
				int rentalId = (int)row["rental_id"];

				Rental rental = new()
                {
					Id = rentalId,
					ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
					ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
					StartDate = row["start_date"] != DBNull.Value ? (DateTime)row["start_date"] : DateTime.MinValue,
					EndDate = row["end_date"] != DBNull.Value ? (DateTime)row["end_date"] : null,
					MonthsUnpaid = row["months_unpaid"] != DBNull.Value ? (int)row["months_unpaid"] : 0
				};

				rentals.Add(rental);
			}

			return rentals;
		}

		public async Task<Rental> GetRentalById(int rentalId)
		{
			if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");

			DataTable rentalTable = await _daoRental.GetRentalById(rentalId);

			if (rentalTable.Rows.Count == 0) throw new ArgumentException("No rental found with the given ID.");

			DataRow row = rentalTable.Rows[0];

			return new Rental
			{
				Id = (int)row["rental_id"],
				ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
				ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
				StartDate = row["start_date"] != DBNull.Value ? (DateTime)row["start_date"] : DateTime.MinValue,
				EndDate = row["end_date"] != DBNull.Value ? (DateTime)row["end_date"] : null,
				MonthsUnpaid = row["months_unpaid"] != DBNull.Value ? (int)row["months_unpaid"] : 0
			};
		}

		public async Task<bool> DeleteRental(int rentalId)
		{
			if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");
			if (await _daoRental.DeleteRental(rentalId)) return true;
			else return false;
		}

        public async Task<int> CreateRentalAsync(Rental rental)
        {
            if (rental == null) throw new ArgumentNullException(nameof(rental), "Rental cannot be null.");
            if (rental.ClientId <= 0) throw new ArgumentException("Invalid client ID.");
            if (rental.ContractedM3 <= 0) throw new ArgumentException("Contracted M3 must be greater than zero.");
            if (rental.StartDate == DateTime.MinValue) throw new ArgumentException("Invalid start date.");
			if (rental.MonthsUnpaid < 0) throw new ArgumentException("Months unpaid cannot be negative.");

            return await _daoRental.CreateRentalAsync(rental);
        }

        public async Task<int> CreateRentalTransactionAsync(Rental rental, SqlConnection connection, SqlTransaction transaction)
        {
            if (rental == null) throw new ArgumentNullException(nameof(rental), "Rental cannot be null.");
            if (rental.ClientId <= 0) throw new ArgumentException("Invalid client ID.");
            if (rental.ContractedM3 <= 0) throw new ArgumentException("Contracted M3 must be greater than zero.");
            if (rental.StartDate == DateTime.MinValue) throw new ArgumentException("Invalid start date.");
			if (rental.MonthsUnpaid < 0) throw new ArgumentException("Months unpaid cannot be negative.");

            return await _daoRental.CreateRentalTransactionAsync(rental, connection, transaction);
        }

		public async Task<List<PendingRentalDTO>> GetPendingPaymentsAsync()
		{
			DataTable pendingRentalTable = await _daoRental.GetPendingPaymentsAsync();
			List<PendingRentalDTO> pendingRentals = [];

			if (pendingRentalTable.Rows.Count == 0)
				return pendingRentals;

			foreach (DataRow row in pendingRentalTable.Rows)
			{
				PendingRentalDTO rental = new()
				{
					Id = (int)row["rental_id"],
					ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
					ClientName = row["client_name"] != DBNull.Value ? row["client_name"].ToString()! : string.Empty,
					PaymentIdentifier = row["payment_identifier"] != DBNull.Value ? Convert.ToDecimal(row["payment_identifier"]) : 0m,
					MonthsUnpaid = row["months_unpaid"] != DBNull.Value ? (int)row["months_unpaid"] : 0,
					CurrentRent = row["CurrentRent"] != DBNull.Value ? Convert.ToDecimal(row["CurrentRent"]) : 0m,
					Balance = row["balance"] != DBNull.Value ? Convert.ToDecimal(row["balance"]) : 0m,
					LockerIdentifiers = row["locker_identifiers"] != DBNull.Value
						? row["locker_identifiers"].ToString()!
						: string.Empty

				};
				pendingRentals.Add(rental);
			}
			return pendingRentals;
		}
		
		public async Task<Rental?> GetRentalByClientIdTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
             if (clientId <= 0) throw new ArgumentException("Invalid client ID.");
             return await _daoRental.GetRentalByClientIdTransactionAsync(clientId, connection, transaction);
        }

        public async Task<bool> UpdateContractedM3TransactionAsync(int rentalId, decimal newM3, SqlConnection connection, SqlTransaction transaction)
        {
             if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");
             if (newM3 < 0) throw new ArgumentException("Contracted M3 cannot be negative."); // O <= 0 si no puede ser cero
             return await _daoRental.UpdateContractedM3TransactionAsync(rentalId, newM3, connection, transaction);
        }
    }
}
