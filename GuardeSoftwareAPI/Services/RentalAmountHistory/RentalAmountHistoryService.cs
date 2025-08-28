using System;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Services.rentalAmountHistory
{

	public class RentalAmountHistoryService : IRentalAmountHistoryService
    {
		readonly DaoRentalAmountHistory _daoRentalAmountHistory;
		public RentalAmountHistoryService(AccessDB accessDB)
		{
			_daoRentalAmountHistory = new DaoRentalAmountHistory(accessDB);
		}

	}
}
