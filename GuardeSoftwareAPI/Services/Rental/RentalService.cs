using System;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.rental
{

	public class RentalService : IRentalService
	{
		readonly DaoRental _daoRental;
		public RentalService(AccessDB accessDB)
		{
			_daoRental = new DaoRental(accessDB);
		}

		public List<Rental> GetRentalsList()
		{
			DataTable rentalTable = _daoRental.GetRentals();
			List<Rental> rentals = new List<Rental>();

			if (rentalTable.Rows.Count == 0) throw new ArgumentException("No rentals found.");

			foreach (DataRow row in rentalTable.Rows)
			{
				int rentalId = (int)row["rental_id"];

				Rental rental = new Rental
				{
					Id = rentalId,
					ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
					ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
					StartDate = row["start_date"] != DBNull.Value ? (DateTime)row["start_date"] : DateTime.MinValue,
					EndDate = row["end_date"] != DBNull.Value ? (DateTime)row["end_date"] : null
				};

				rentals.Add(rental);
			}

			return rentals;
		}

		public Rental GetRentalById(int rentalId)
		{
			if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");

			DataTable rentalTable = _daoRental.GetRentalById(rentalId);

			if (rentalTable.Rows.Count == 0) throw new ArgumentException("No rental found with the given ID.");

			DataRow row = rentalTable.Rows[0];

			return new Rental
			{
				Id = (int)row["rental_id"],
				ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
				ContractedM3 = row["contracted_m3"] != DBNull.Value ? Convert.ToDecimal(row["contracted_m3"]) : 0m,
				StartDate = row["start_date"] != DBNull.Value ? (DateTime)row["start_date"] : DateTime.MinValue,
				EndDate = row["end_date"] != DBNull.Value ? (DateTime)row["end_date"] : null
			};
		}

        public async Task<int> CreateRentalAsync(Rental rental)
		{
			if (rental == null) throw new ArgumentNullException(nameof(rental), "Rental cannot be null.");
			if (rental.ClientId <= 0) throw new ArgumentException("Invalid client ID.");
			if (rental.ContractedM3 <= 0) throw new ArgumentException("Contracted M3 must be greater than zero.");
			if (rental.StartDate == DateTime.MinValue) throw new ArgumentException("Invalid start date.");

            return await _daoRental.CreateRentalAsync(rental);
        }

		public bool DeleteRental(int rentalId)
		{
			if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");
			if (_daoRental.DeleteRental(rentalId)) return true;
			else return false;
		}
		
	}
}
