using System;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.rentalAmountHistory
{

	public interface IRentalAmountHistoryService
	{
		public List<RentalAmountHistory> GetRentalAmountHistoriesList();

		public RentalAmountHistory GetRentalAmountHistoryByRentalId(int id);

		public bool CreateRentalAmountHistory(RentalAmountHistory rentalAmountHistory);

		Task<int> CreateRentalAmountHistoryAsync(RentalAmountHistory rentalAmountHistory);

        Task<int> CreateRentalAmountHistoryTransactionAsync(RentalAmountHistory rentalAmountHistory, SqlConnection connection, SqlTransaction transaction);
    }
}