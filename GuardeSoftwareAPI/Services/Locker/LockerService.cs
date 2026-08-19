using System;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Dtos.Locker;
using GuardeSoftwareAPI.Services.activityLog;
using System.Text.Json;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Services.locker
{

	public class LockerService : ILockerService
    {
		private readonly DaoLocker daoLocker;
		private readonly AccessDB _accessDB;
		private readonly IActivityLogService _activityLogService;

		public LockerService(AccessDB accessDB, IActivityLogService activityLogService)
		{
			daoLocker = new DaoLocker(accessDB);
			_accessDB = accessDB;
			_activityLogService = activityLogService;
		}

		public async Task<List<Locker>> GetLockersList() {

			DataTable LockerTable = await daoLocker.GetLockers();
			List<Locker> lockersList = [];

			foreach (DataRow row in LockerTable.Rows) {

				Locker locker = new()
                {
					Id = row.Field<int>("locker_id"),
                    WarehouseId = row.Field<int>("warehouse_id"),
                    LockerTypeId = row.Field<int>("locker_type_id"),
                    RentalId = row["rental_id"] != DBNull.Value ? (int?)row["rental_id"] : null,
					Identifier = row["identifier"]?.ToString() ?? string.Empty,
                    Features = row["features"]?.ToString() ?? string.Empty,
                    Status = row["status"]?.ToString() ?? string.Empty,
                    ClientName = row["client_name"]?.ToString() ?? string.Empty,
                    ClientNames = row["client_names"]?.ToString() ?? string.Empty,
                    IsFreeSpace = row["is_free_space"] != DBNull.Value && Convert.ToBoolean(row["is_free_space"])
                };	
				lockersList.Add(locker);
			}
			return lockersList;
		}

        public async Task<List<Locker>> GetLockerListById(int id)
        {

            DataTable LockerTable = await daoLocker.GetLockerById(id);
            List<Locker> lockersList = [];

            foreach (DataRow row in LockerTable.Rows)
            {

                Locker locker = new()
                {
                    Id = row.Field<int>("locker_id"),
                    WarehouseId = row.Field<int>("warehouse_id"),
                    LockerTypeId = row.Field<int>("locker_type_id"),
                    RentalId = row["rental_id"] != DBNull.Value ? (int?)row["rental_id"] : null, 
                    Identifier = row["identifier"]?.ToString() ?? string.Empty,
                    Features = row["features"]?.ToString() ?? string.Empty,
                    Status = row["status"]?.ToString() ?? string.Empty,
                    IsFreeSpace = row["is_free_space"] != DBNull.Value && Convert.ToBoolean(row["is_free_space"])
                };
                lockersList.Add(locker);
            }
            return lockersList;
        }

        public async Task<Locker> CreateLocker(Locker locker)
        {
            if (locker == null)
                throw new ArgumentNullException(nameof(locker));

            if (locker.WarehouseId <= 0)
                throw new ArgumentException("Invalid WareHouse ID.");

            if (locker.LockerTypeId <= 0)
                throw new ArgumentException("Invalid Locker Type ID.");

            locker.Identifier = string.IsNullOrWhiteSpace(locker.Identifier)
                                ? null
                                : locker.Identifier.Trim();

            locker.Features = string.IsNullOrWhiteSpace(locker.Features)
                            ? null
                            : locker.Features.Trim();

            if (string.IsNullOrWhiteSpace(locker.Status))
                throw new ArgumentException("Locker status is required.");

            // Los espacios libres siempre arrancan como DISPONIBLE
            if (locker.IsFreeSpace)
                locker.Status = "DISPONIBLE";

			Locker created = await daoLocker.CreateLocker(locker);
			await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
			{
				Action = "CREATE",
				TableName = "lockers",
				RecordId = created.Id,
				NewValue = JsonSerializer.Serialize(new
				{
					created.Id,
					created.WarehouseId,
					created.LockerTypeId,
					created.Identifier,
					created.Features,
					created.Status,
					created.IsFreeSpace
				})
			});
			return created;
        }

        public async Task<bool> SetRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");

            if (lockerIds == null || lockerIds.Count == 0)
                throw new ArgumentException("At least one lockerId must be provided.", nameof(lockerIds));

            if (lockerIds.Any(id => id <= 0))
                throw new ArgumentException("All lockerIds must be positive integers.", nameof(lockerIds));

            if (lockerIds.Distinct().Count() != lockerIds.Count)
                throw new ArgumentException("Duplicate lockerIds are not allowed.", nameof(lockerIds));

            return await daoLocker.SetRentalTransactionAsync(rentalId, lockerIds, connection, transaction); 
        }

        public async Task<List<GetLockerClientDetailDTO>> GetLockersByClientIdAsync(int clientId)
        {
            DataTable lockersTable = await daoLocker.GetLockersByClientIdAsync(clientId);
            List<GetLockerClientDetailDTO> lockersList = [];

            foreach (DataRow row in lockersTable.Rows)
            {
                GetLockerClientDetailDTO lockerDto = new()
                {
                    Id = Convert.ToInt32(row["locker_id"]),
                    Identifier = row["identifier"]?.ToString() ?? string.Empty,
                    Warehouse = row["warehouse"]?.ToString() ?? string.Empty,
                    LockerType = row["locker_type"]?.ToString() ?? string.Empty,
                    Features = row["features"]?.ToString() ?? string.Empty,
                    // M3 = Convert.ToDecimal(row["m3"]), // 
                };
                lockersList.Add(lockerDto);
            }

            return lockersList;
        }

        public async Task<bool> DeleteLocker(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid Locker Id.");

			Locker? previous = (await GetLockerListById(id)).FirstOrDefault();
			if (await daoLocker.DeleteLocker(id))
			{
				await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
				{
					Action = "DELETE",
					TableName = "lockers",
					RecordId = id,
					OldValue = previous == null ? null : JsonSerializer.Serialize(previous),
					NewValue = JsonSerializer.Serialize(new { Active = false })
				});
				return true;
			}
			else return false;
        }

        public async Task<bool> IsLockerAvailableAsync(int lockerId, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerId <= 0)
                throw new ArgumentException("Invalid locker ID.", nameof(lockerId));

            return await daoLocker.IsLockerIsAvailabeAsync(lockerId, connection, transaction);
        }

        public async Task<bool> UpdateLocker(int Id, UpdateLockerDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (Id <= 0)
                throw new ArgumentException("Invalid lockerId.");

            if (string.IsNullOrWhiteSpace(dto.Identifier))
                throw new ArgumentException("Locker identifier is required.");

            if (string.IsNullOrWhiteSpace(dto.Status))
                throw new ArgumentException("Locker Status is required.");

            if (dto.LockerTypeId <= 0)
                throw new ArgumentException("Invalid Locker Type ID.");

            if (dto.WarehouseId <= 0)
                throw new ArgumentException("Invalid Warehouse ID.");

            var Locker = new Locker
            {
                Id = Id,
                Identifier = dto.Identifier,
                Features = dto.Features,
                Status = dto.Status,
                LockerTypeId = dto.LockerTypeId,
                WarehouseId = dto.WarehouseId,
                IsFreeSpace = dto.IsFreeSpace
            };

			Locker? previous = (await GetLockerListById(Id)).FirstOrDefault();
			bool updated = await ProcessLockerUnassignmentIfAvailableAsync(Id, dto.Status, Locker);
			if (updated)
			{
				await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
				{
					Action = "UPDATE",
					TableName = "lockers",
					RecordId = Id,
					OldValue = previous == null ? null : JsonSerializer.Serialize(previous),
					NewValue = JsonSerializer.Serialize(Locker)
				});
			}
			return updated;
        }

        public async Task<bool> UpdateLockerStatus(int lockerId, UpdateLockerStatusDto dto)
        {
            if (lockerId <= 0) throw new ArgumentException("Invalid locker ID.");
            if (string.IsNullOrWhiteSpace(dto.Status)) throw new ArgumentException("Status is required.");


			Locker? previous = (await GetLockerListById(lockerId)).FirstOrDefault();
			bool updated = await ProcessLockerUnassignmentIfAvailableAsync(lockerId, dto.Status, null);
			if (updated)
			{
				await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
				{
					Action = "UPDATE",
					TableName = "lockers",
					RecordId = lockerId,
					OldValue = previous == null ? null : JsonSerializer.Serialize(previous),
					NewValue = JsonSerializer.Serialize(new { Id = lockerId, Status = dto.Status })
				});
			}
			return updated;
        }

        private async Task<bool> ProcessLockerUnassignmentIfAvailableAsync(int lockerId, string newStatus, Locker? fullLockerUpdate)
        {
            DataTable existingDt = await daoLocker.GetLockerById(lockerId);
            if (existingDt.Rows.Count == 0)
                return false;

            DataRow existing = existingDt.Rows[0];
            int? existingRentalId = existing["rental_id"] != DBNull.Value ? Convert.ToInt32(existing["rental_id"]) : null;
            bool isFreeSpace = existing["is_free_space"] != DBNull.Value && Convert.ToBoolean(existing["is_free_space"]);

            // Espacios libres: al ponerlos OCUPADO manualmente, eliminar todas sus asignaciones en rental_lockers.
            // Bauleras normales: si se pone DISPONIBLE y tenía rental_id, desasignar.
            bool needsUnassignment = isFreeSpace
                ? newStatus.Equals("OCUPADO", StringComparison.OrdinalIgnoreCase)
                : (existingRentalId.HasValue && newStatus.Equals("DISPONIBLE", StringComparison.OrdinalIgnoreCase));

            if (needsUnassignment)
            {
                using var connection = _accessDB.GetConnectionClose();
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();
                try
                {
                    if (isFreeSpace)
                    {
                        // Obtener todos los rentals asignados a este espacio libre
                        var rentalIds = await GetRentalIdsForFreeSpaceAsync(lockerId, connection, transaction);

                        // Eliminar todas las asignaciones del espacio libre en rental_lockers
                        await daoLocker.UnassignFreeSpaceFromRentalTransactionAsync(0, [lockerId], connection, transaction);
                        // Nota: pasamos 0 como rentalId para forzar eliminación de TODAS las asignaciones
                        // (la implementación de UnassignFreeSpaceFromRentalTransactionAsync con rentalId=0 elimina por locker_id solamente)

                        var daoClient = new DaoClient(_accessDB);
                        var daoRental = new DaoRental(_accessDB);
                        foreach (var rentalId in rentalIds)
                        {
                            DataTable rentalDt = await daoRental.GetRentalById(rentalId);
                            if (rentalDt.Rows.Count > 0)
                            {
                                int clientId = Convert.ToInt32(rentalDt.Rows[0]["client_id"]);
                                await daoClient.CloseLockerHistoryTransactionAsync(clientId, [lockerId], connection, transaction);
                            }
                        }

                        if (fullLockerUpdate != null)
                            await daoLocker.UpdateLockerTransactionAsync(fullLockerUpdate, false, connection, transaction);
                        else
                            await daoLocker.UpdateLockerStatus(lockerId, "OCUPADO");
                    }
                    else
                    {
                        // Baulera normal: comportamiento original
                        if (fullLockerUpdate != null)
                        {
                            fullLockerUpdate.Status = "DISPONIBLE";
                            await daoLocker.UpdateLockerTransactionAsync(fullLockerUpdate, true, connection, transaction);
                        }
                        else
                        {
                            await daoLocker.UnassignLockersFromRentalTransactionAsync(existingRentalId!.Value, [lockerId], connection, transaction);
                        }

                        var daoRental = new DaoRental(_accessDB);
                        var daoClient = new DaoClient(_accessDB);
                        DataTable rentalDt = await daoRental.GetRentalById(existingRentalId!.Value);
                        if (rentalDt.Rows.Count > 0)
                        {
                            int clientId = Convert.ToInt32(rentalDt.Rows[0]["client_id"]);
                            await daoClient.CloseLockerHistoryTransactionAsync(clientId, [lockerId], connection, transaction);
                        }

                        var remainingLockerIds = await daoLocker.GetLockerIdsByRentalIdTransactionAsync(existingRentalId!.Value, connection, transaction);
                        decimal newContractedM3 = await daoLocker.CalculateTotalM3ForLockersAsync(remainingLockerIds, connection, transaction);
                        await daoRental.UpdateContractedM3TransactionAsync(existingRentalId!.Value, newContractedM3, connection, transaction);

                        var cmbService = new GuardeSoftwareAPI.Services.clientMonthBalance.ClientMonthBalanceService(_accessDB);
                        await cmbService.RebuildForRentalTransactionAsync(existingRentalId!.Value, connection, transaction);
                    }

                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            else
            {
                if (fullLockerUpdate != null)
                {
                    return await daoLocker.UpdateLocker(fullLockerUpdate);
                }
                else
                {
                    return await daoLocker.UpdateLockerStatus(lockerId, newStatus);
                }
            }
        }

        private async Task<List<int>> GetRentalIdsForFreeSpaceAsync(int lockerId, SqlConnection connection, SqlTransaction transaction)
        {
            var ids = new List<int>();
            const string query = "SELECT rental_id FROM rental_lockers WHERE locker_id = @locker_id";
            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                ids.Add(reader.GetInt32(0));
            return ids;
        }

        public async Task<List<int>> GetLockerIdsByRentalIdTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            return await daoLocker.GetLockerIdsByRentalIdTransactionAsync(rentalId, connection, transaction);
        }

        public async Task<bool> UnassignLockersFromRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
             if (lockerIds == null || !lockerIds.Any()) return true; // Nada que hacer
             // Validar IDs si es necesario
             int rowsAffected = await daoLocker.UnassignLockersFromRentalTransactionAsync(rentalId, lockerIds, connection, transaction);
             return rowsAffected == lockerIds.Count; // Verifica si se desasignaron todos los esperados
        }

        public async Task<bool> AssignLockersToRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || !lockerIds.Any()) return true; // Nada que hacer
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID for assignment.");
             // Validar IDs si es necesario
             // La verificación de disponibilidad ya se hace en UpdateClientAsync
             int rowsAffected = await daoLocker.AssignLockersToRentalTransactionAsync(rentalId, lockerIds, connection, transaction);
              return rowsAffected == lockerIds.Count; // Verifica si se asignaron todos los esperados
        }

        public async Task<decimal> CalculateTotalM3ForLockersAsync(List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
             if (lockerIds == null || !lockerIds.Any()) return 0m;
             // Validar IDs si es necesario
             return await daoLocker.CalculateTotalM3ForLockersAsync(lockerIds, connection, transaction);
        }

    }
}
