using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Services.rentalAmountHistory
{

	public class RentalAmountHistoryService : IRentalAmountHistoryService
	{
		readonly DaoRentalAmountHistory _daoRentalAmountHistory;
		public RentalAmountHistoryService(AccessDB accessDB)
		{
			_daoRentalAmountHistory = new DaoRentalAmountHistory(accessDB);
		}

		public async Task<List<RentalAmountHistory>> GetRentalAmountHistoriesList()
		{
			DataTable rentalAmountHistoryTable = await _daoRentalAmountHistory.GetRentalAmountHistoriesList();
			List<RentalAmountHistory> rentalAmountHistories = new List<RentalAmountHistory>();

			if (rentalAmountHistoryTable.Rows.Count == 0) throw new ArgumentException("No rental amount histories found.");

			foreach (DataRow row in rentalAmountHistoryTable.Rows)
			{
				int rentalAmountHistoryId = (int)row["rental_amount_history_id"];

				RentalAmountHistory rentalAmountHistory = new RentalAmountHistory
				{
					Id = rentalAmountHistoryId,
					RentalId = row["rental_id"] != DBNull.Value ? (int)row["rental_id"] : 0,
					Amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0m,
					StartDate = row["start_date"] != DBNull.Value ? (DateTime)row["start_date"] : DateTime.MinValue,
					EndDate = row["end_date"] != DBNull.Value ? (DateTime)row["end_date"] : null
				};

				rentalAmountHistories.Add(rentalAmountHistory);
			}

			return rentalAmountHistories;
		}

		public async Task<RentalAmountHistory> GetRentalAmountHistoryByRentalId(int id)
		{
			if (id <= 0) throw new ArgumentException("Invalid rental amount history ID.");

			DataTable rentalAmountHistoryTable = await _daoRentalAmountHistory.GetRentalAmountHistoryByRentalId(id);

			if (rentalAmountHistoryTable.Rows.Count == 0) throw new ArgumentException("No rental amouny history found with the given ID.");

			DataRow row = rentalAmountHistoryTable.Rows[0];

			return new RentalAmountHistory
			{
				Id = (int)row["rental_amount_history_id"],
				RentalId = row["rental_id"] != DBNull.Value ? (int)row["rental_id"] : 0,
				Amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0m,
				StartDate = row["start_date"] != DBNull.Value ? (DateTime)row["start_date"] : DateTime.MinValue,
				EndDate = row["end_date"] != DBNull.Value ? (DateTime)row["end_date"] : null
			};
		}

		public async Task<bool> CreateRentalAmountHistory(RentalAmountHistory rentalAmountHistory)
		{
			if (rentalAmountHistory == null) throw new ArgumentNullException(nameof(rentalAmountHistory), "Rental amount history cannot be null.");
			if (rentalAmountHistory.RentalId <= 0) throw new ArgumentException("Invalid rental ID.");
			if (rentalAmountHistory.Amount <= 0) throw new ArgumentException("Amount must be greater than zero.");
			if (rentalAmountHistory.StartDate == DateTime.MinValue) throw new ArgumentException("Invalid start date.");
			if (await _daoRentalAmountHistory.CreateRentalAmountHistory(rentalAmountHistory)) return true;
			else return false;
		}

        public async Task<int> CreateRentalAmountHistoryAsync(RentalAmountHistory rentalAmountHistory)
        {
            if (rentalAmountHistory == null) throw new ArgumentNullException(nameof(rentalAmountHistory), "Rental amount history cannot be null.");
            if (rentalAmountHistory.RentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            if (rentalAmountHistory.Amount <= 0) throw new ArgumentException("Amount must be greater than zero.");
            if (rentalAmountHistory.StartDate == DateTime.MinValue) throw new ArgumentException("Invalid start date.");

            return await _daoRentalAmountHistory.CreateRentalAmountHistoryAsync(rentalAmountHistory);
        }


        public async Task<int> CreateRentalAmountHistoryTransactionAsync(RentalAmountHistory rentalAmountHistory, SqlConnection connection, SqlTransaction transaction)
        {
            if (rentalAmountHistory == null) throw new ArgumentNullException(nameof(rentalAmountHistory), "Rental amount history cannot be null.");
            if (rentalAmountHistory.RentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            if (rentalAmountHistory.Amount <= 0) throw new ArgumentException("Amount must be greater than zero.");
            if (rentalAmountHistory.StartDate == DateTime.MinValue) throw new ArgumentException("Invalid start date.");

            return await _daoRentalAmountHistory.CreateRentalAmountHistoryTransactionAsync(rentalAmountHistory, connection, transaction);
        }


    }
}
