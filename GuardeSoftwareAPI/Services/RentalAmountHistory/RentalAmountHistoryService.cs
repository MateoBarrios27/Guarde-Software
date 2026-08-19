using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Services.activityLog;
using System.Text.Json;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Services.rentalAmountHistory
{

	public class RentalAmountHistoryService : IRentalAmountHistoryService
	{
		readonly DaoRentalAmountHistory _daoRentalAmountHistory;
		private readonly IActivityLogService _activityLogService;

		public RentalAmountHistoryService(AccessDB accessDB, IActivityLogService activityLogService)
		{
			_daoRentalAmountHistory = new DaoRentalAmountHistory(accessDB);
			_activityLogService = activityLogService;
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

		public async Task<RentalAmountHistory> CreateRentalAmountHistory(RentalAmountHistory rentalAmountHistory)
		{
			if (rentalAmountHistory == null) throw new ArgumentNullException(nameof(rentalAmountHistory), "Rental amount history cannot be null.");
			if (rentalAmountHistory.RentalId <= 0) throw new ArgumentException("Invalid rental ID.");
			if (rentalAmountHistory.Amount < 0) throw new ArgumentException("Amount must be greater than zero.");
			if (rentalAmountHistory.StartDate == DateTime.MinValue) throw new ArgumentException("Invalid start date.");
			RentalAmountHistory created = await _daoRentalAmountHistory.CreateRentalAmountHistory(rentalAmountHistory);
			await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
			{
				Action = "CREATE",
				TableName = "rental_amount_history",
				RecordId = created.Id,
				NewValue = JsonSerializer.Serialize(new { created.Id, created.RentalId, created.Amount, created.StartDate, created.EndDate })
			});
			return created;
		}

        public async Task<int> CreateRentalAmountHistoryAsync(RentalAmountHistory rentalAmountHistory)
        {
            if (rentalAmountHistory == null) throw new ArgumentNullException(nameof(rentalAmountHistory), "Rental amount history cannot be null.");
            if (rentalAmountHistory.RentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            if (rentalAmountHistory.Amount < 0) throw new ArgumentException("Amount must be greater than zero.");
            if (rentalAmountHistory.StartDate == DateTime.MinValue) throw new ArgumentException("Invalid start date.");

			int historyId = await _daoRentalAmountHistory.CreateRentalAmountHistoryAsync(rentalAmountHistory);
			await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
			{
				Action = "CREATE",
				TableName = "rental_amount_history",
				RecordId = historyId,
				NewValue = JsonSerializer.Serialize(new { Id = historyId, rentalAmountHistory.RentalId, rentalAmountHistory.Amount, rentalAmountHistory.StartDate, rentalAmountHistory.EndDate })
			});
			return historyId;
        }


		public async Task<int> CreateRentalAmountHistoryTransactionAsync(RentalAmountHistory rentalAmountHistory, SqlConnection connection, SqlTransaction transaction)
        {
            if (rentalAmountHistory == null) throw new ArgumentNullException(nameof(rentalAmountHistory));
            if (rentalAmountHistory.RentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            if (rentalAmountHistory.Amount < 0) throw new ArgumentException("Amount must be greater than zero.");
            if (rentalAmountHistory.StartDate == default) throw new ArgumentException("Invalid start date.");

            var historyId = await _daoRentalAmountHistory.CreateRentalAmountHistoryTransactionAsync(rentalAmountHistory, connection, transaction);
            await NormalizeRentalAmountHistoryTransactionAsync(rentalAmountHistory.RentalId, connection, transaction);
            return historyId;
        }

        public async Task<RentalAmountHistory?> GetLatestRentalAmountHistoryTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            return await _daoRentalAmountHistory.GetLatestRentalAmountHistoryTransactionAsync(rentalId, connection, transaction);
        }

        public async Task UpsertRentalAmountHistoryTransactionAsync(
            int rentalId,
            decimal amount,
            DateTime startDate,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            if (amount < 0) throw new ArgumentException("El monto tiene que ser 0 o positivo.");

            // Los cambios de abono se expresan por fecha de inicio. Si el mismo
            // tramo se confirma dos veces, actualizamos el registro existente en
            // lugar de apilar otra fila para la misma fecha.
            var normalizedStart = startDate.Date;
            const string existingQuery = @"
                SELECT TOP 1 rental_amount_history_id
                FROM rental_amount_history
                WHERE rental_id = @rental_id
                  AND CAST(start_date AS date) = @start_date
                ORDER BY rental_amount_history_id DESC";

            int? existingId = null;
            using (var command = new SqlCommand(existingQuery, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                command.Parameters.Add(new SqlParameter("@start_date", SqlDbType.Date) { Value = normalizedStart });
                var value = await command.ExecuteScalarAsync();
                if (value != null && value != DBNull.Value)
                    existingId = Convert.ToInt32(value);
            }

            if (existingId.HasValue)
            {
                const string updateQuery = @"
                    UPDATE rental_amount_history
                    SET amount = @amount,
                        start_date = @start_date
                    WHERE rental_amount_history_id = @history_id";
                using var update = new SqlCommand(updateQuery, connection, transaction);
                update.Parameters.Add(new SqlParameter("@amount", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Value = amount
                });
                update.Parameters.Add(new SqlParameter("@start_date", SqlDbType.Date) { Value = normalizedStart });
                update.Parameters.Add(new SqlParameter("@history_id", SqlDbType.Int) { Value = existingId.Value });
                await update.ExecuteNonQueryAsync();

                // Si ya había duplicados para esa fecha, conservamos el último
                // registro (el que acabamos de actualizar) y eliminamos los demás.
                const string deleteDuplicatesQuery = @"
                    DELETE FROM rental_amount_history
                    WHERE rental_id = @rental_id
                      AND CAST(start_date AS date) = @start_date
                      AND rental_amount_history_id <> @history_id";
                using var deleteDuplicates = new SqlCommand(deleteDuplicatesQuery, connection, transaction);
                deleteDuplicates.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                deleteDuplicates.Parameters.Add(new SqlParameter("@start_date", SqlDbType.Date) { Value = normalizedStart });
                deleteDuplicates.Parameters.Add(new SqlParameter("@history_id", SqlDbType.Int) { Value = existingId.Value });
                await deleteDuplicates.ExecuteNonQueryAsync();
            }
            else
            {
                await _daoRentalAmountHistory.CreateRentalAmountHistoryTransactionAsync(new RentalAmountHistory
                {
                    RentalId = rentalId,
                    Amount = amount,
                    StartDate = normalizedStart
                }, connection, transaction);
            }

            await NormalizeRentalAmountHistoryTransactionAsync(rentalId, connection, transaction);
        }

        public async Task NormalizeRentalAmountHistoryTransactionAsync(
            int rentalId,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");

            const string query = @"
                SELECT rental_amount_history_id, amount, start_date
                FROM rental_amount_history
                WHERE rental_id = @rental_id
                ORDER BY start_date ASC, rental_amount_history_id ASC";

            var rows = new List<(int Id, DateTime StartDate)>();
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add((reader.GetInt32(0), reader.GetDateTime(2)));
                }
            }

            if (rows.Count == 0) return;

            var canonicalRows = rows
                .GroupBy(row => row.StartDate.Date)
                .Select(group => group.OrderByDescending(row => row.Id).First())
                .OrderBy(row => row.StartDate)
                .ThenBy(row => row.Id)
                .ToList();

            var duplicateIds = rows
                .Select(row => row.Id)
                .Except(canonicalRows.Select(row => row.Id))
                .ToArray();

            foreach (var duplicateId in duplicateIds)
            {
                const string deleteQuery = "DELETE FROM rental_amount_history WHERE rental_amount_history_id = @history_id";
                using var deleteCommand = new SqlCommand(deleteQuery, connection, transaction);
                deleteCommand.Parameters.Add(new SqlParameter("@history_id", SqlDbType.Int) { Value = duplicateId });
                await deleteCommand.ExecuteNonQueryAsync();
            }

            for (var index = 0; index < canonicalRows.Count; index++)
            {
                var current = canonicalRows[index];
                DateTime? endDate = index + 1 < canonicalRows.Count
                    ? canonicalRows[index + 1].StartDate.Date.AddSeconds(-1)
                    : null;

                const string updateQuery = @"
                    UPDATE rental_amount_history
                    SET end_date = @end_date
                    WHERE rental_amount_history_id = @history_id";
                using var updateCommand = new SqlCommand(updateQuery, connection, transaction);
                updateCommand.Parameters.Add(new SqlParameter("@end_date", SqlDbType.DateTime)
                {
                    Value = (object?)endDate ?? DBNull.Value
                });
                updateCommand.Parameters.Add(new SqlParameter("@history_id", SqlDbType.Int) { Value = current.Id });
                await updateCommand.ExecuteNonQueryAsync();
            }
        }

        public async Task RemoveOrphanedPlannedHistoriesTransactionAsync(
            int rentalId,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");

            var histories = new List<(int Id, DateTime StartDate)>();
            const string historyQuery = @"
                SELECT rental_amount_history_id, start_date
                FROM rental_amount_history
                WHERE rental_id = @rental_id
                ORDER BY start_date ASC, rental_amount_history_id ASC";
            using (var historyCommand = new SqlCommand(historyQuery, connection, transaction))
            {
                historyCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                using var reader = await historyCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    histories.Add((reader.GetInt32(0), reader.GetDateTime(1).Date));
            }

            if (histories.Count == 0) return;

            var plannedMonths = new List<DateTime>();
            const string plannedQuery = @"
                SELECT DISTINCT DATEFROMPARTS(YEAR(movement_date), MONTH(movement_date), 1)
                FROM account_movements
                WHERE rental_id = @rental_id
                  AND movement_type = 'DEBITO'
                  AND payment_id IS NULL
                  AND concept LIKE 'Alquiler % (Planificado)'";
            using (var plannedCommand = new SqlCommand(plannedQuery, connection, transaction))
            {
                plannedCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                using var reader = await plannedCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    plannedMonths.Add(reader.GetDateTime(0).Date);
            }

            var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var orphanedIds = new List<int>();
            for (var index = 0; index < histories.Count; index++)
            {
                var history = histories[index];
                if (history.StartDate <= currentMonth) continue;

                var nextStart = index + 1 < histories.Count
                    ? histories[index + 1].StartDate
                    : (DateTime?)null;
                var isUsedByPlan = plannedMonths.Any(month =>
                    month >= new DateTime(history.StartDate.Year, history.StartDate.Month, 1)
                    && (!nextStart.HasValue || month < new DateTime(nextStart.Value.Year, nextStart.Value.Month, 1)));

                if (!isUsedByPlan)
                    orphanedIds.Add(history.Id);
            }

            foreach (var historyId in orphanedIds)
            {
                const string deleteQuery = "DELETE FROM rental_amount_history WHERE rental_amount_history_id = @history_id";
                using var deleteCommand = new SqlCommand(deleteQuery, connection, transaction);
                deleteCommand.Parameters.Add(new SqlParameter("@history_id", SqlDbType.Int) { Value = historyId });
                await deleteCommand.ExecuteNonQueryAsync();
            }

            if (orphanedIds.Count > 0)
                await NormalizeRentalAmountHistoryTransactionAsync(rentalId, connection, transaction);
        }

        public async Task EndAndCreateRentalAmountHistoryTransactionAsync(int oldHistoryId, int rentalId, decimal newAmount, DateTime startDate, SqlConnection connection, SqlTransaction transaction)
        {
            if (oldHistoryId <= 0) throw new ArgumentException("Invalid old history ID.");
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            if (newAmount < 0) throw new ArgumentException("El monto tiene que ser 0 o positivo.");
            if (startDate == default) throw new ArgumentException("Fecha de inicio inválida.");

            await UpsertRentalAmountHistoryTransactionAsync(rentalId, newAmount, startDate, connection, transaction);
        }

		public async Task CloseOpenHistoriesByRentalIdTransactionAsync(int rentalId, DateTime endDate, SqlConnection connection, SqlTransaction transaction)
        {
            await _daoRentalAmountHistory.CloseOpenHistoriesByRentalIdTransactionAsync(rentalId, endDate, connection, transaction);
        }

    }
}
