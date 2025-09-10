using System;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.rental
{

	public interface IRentalService
	{
		public List<Rental> GetRentalsList();

		public Rental GetRentalById(int id);

		public bool DeleteRental(int rentalId);

        Task<int> CreateRentalAsync(Rental rental);

        Task<int> CreateRentalTransactionAsync(Rental rental, SqlConnection connection, SqlTransaction transaction);

    }
}