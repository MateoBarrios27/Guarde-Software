using System;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.rental
{

	public interface IRentalService
	{
		Task<List<Rental>> GetRentalsList();

		Task<Rental> GetRentalById(int id);

		Task<bool> DeleteRental(int rentalId);

        Task<int> CreateRentalAsync(Rental rental);

        Task<int> CreateRentalTransactionAsync(Rental rental, SqlConnection connection, SqlTransaction transaction);

    }
}