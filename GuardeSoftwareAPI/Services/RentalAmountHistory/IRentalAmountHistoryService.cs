using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.rentalAmountHistory
{

	public interface IRentalAmountHistoryService
	{
		public List<RentalAmountHistory> GetRentalAmountHistoriesList();
		public RentalAmountHistory GetRentalAmountHistoryByRentalId(int id);
		public bool CreateRentalAmountHistory(RentalAmountHistory rentalAmountHistory);
	}
}