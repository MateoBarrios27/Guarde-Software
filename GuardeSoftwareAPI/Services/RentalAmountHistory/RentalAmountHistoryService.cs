using System;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.rentalAmountHistory
{

	public class RentalAmountHistoryService : IRentalAmountHistoryService
	{
		readonly DaoRentalAmountHistory _daoRentalAmountHistory;
		public RentalAmountHistoryService(AccessDB accessDB)
		{
			_daoRentalAmountHistory = new DaoRentalAmountHistory(accessDB);
		}

		public List<RentalAmountHistory> GetRentalAmountHistoriesList()
		{
			DataTable rentalAmountHistoryTable = _daoRentalAmountHistory.GetRentalAmountHistoriesList();
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

		public RentalAmountHistory GetRentalAmountHistoryByRentalId(int id)
		{
			if (id <= 0) throw new ArgumentException("Invalid rental amount history ID.");

			DataTable rentalAmountHistoryTable = _daoRentalAmountHistory.GetRentalAmountHistoryByRentalId(id);

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
	}
}
