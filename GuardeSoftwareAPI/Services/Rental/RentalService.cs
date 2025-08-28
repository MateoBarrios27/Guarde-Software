using System;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Services.rental
{

	public class RentalService : IRentalService
    {
		readonly DaoRental _daoRental;
		public RentalService(AccessDB accessDB)
		{
			_daoRental = new DaoRental(accessDB);
		}
	}
}
