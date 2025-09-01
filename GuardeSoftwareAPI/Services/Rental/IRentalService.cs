using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.rental
{

	public interface IRentalService
	{
		public List<Rental> GetRentalsList();
		public Rental GetRentalById(int id);
		public bool DeleteRental(int rentalId);
		public bool CreateRental(Rental rental);
	}
}