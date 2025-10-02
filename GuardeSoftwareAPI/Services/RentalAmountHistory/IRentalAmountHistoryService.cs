using System;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.rentalAmountHistory
{

	public interface IRentalAmountHistoryService
	{
		Task<List<RentalAmountHistory>> GetRentalAmountHistoriesList();

		Task<RentalAmountHistory> GetRentalAmountHistoryByRentalId(int id);

		Task<RentalAmountHistory> CreateRentalAmountHistory(RentalAmountHistory rentalAmountHistory);

		Task<int> CreateRentalAmountHistoryAsync(RentalAmountHistory rentalAmountHistory);

        Task<int> CreateRentalAmountHistoryTransactionAsync(RentalAmountHistory rentalAmountHistory, SqlConnection connection, SqlTransaction transaction);
    }
}