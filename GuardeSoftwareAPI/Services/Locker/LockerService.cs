using System;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Dtos.Locker;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Services.locker
{

	public class LockerService : ILockerService
    {
		private readonly DaoLocker daoLocker;
		private readonly AccessDB _accessDB;

		public LockerService(AccessDB accessDB)
		{
			daoLocker = new DaoLocker(accessDB);
			_accessDB = accessDB;
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
                    Features =row["features"]?.ToString() ?? string.Empty,
                    Status = row["status"]?.ToString() ?? string.Empty,
                    ClientName = row["client_name"]?.ToString() ?? string.Empty
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

            return await daoLocker.CreateLocker(locker);
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

            if (await daoLocker.DeleteLocker(id)) return true;
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
                WarehouseId = dto.WarehouseId
            };

            return await ProcessLockerUnassignmentIfAvailableAsync(Id, dto.Status, Locker);  
        }

        public async Task<bool> UpdateLockerStatus(int lockerId, UpdateLockerStatusDto dto)
        {
            if (lockerId <= 0) throw new ArgumentException("Invalid locker ID.");
            if (string.IsNullOrWhiteSpace(dto.Status)) throw new ArgumentException("Status is required.");


            return await ProcessLockerUnassignmentIfAvailableAsync(lockerId, dto.Status, null);
        }

        private async Task<bool> ProcessLockerUnassignmentIfAvailableAsync(int lockerId, string newStatus, Locker? fullLockerUpdate)
        {
            DataTable existingDt = await daoLocker.GetLockerById(lockerId);
            int? existingRentalId = null;
            if (existingDt.Rows.Count > 0 && existingDt.Rows[0]["rental_id"] != DBNull.Value)
            {
                existingRentalId = Convert.ToInt32(existingDt.Rows[0]["rental_id"]);
            }

            if (existingRentalId.HasValue && newStatus.Equals("DISPONIBLE", StringComparison.OrdinalIgnoreCase))
            {
                using var connection = _accessDB.GetConnectionClose();
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();
                try
                {
                    if (fullLockerUpdate != null)
                    {
                        fullLockerUpdate.Status = "DISPONIBLE";
                        await daoLocker.UpdateLockerTransactionAsync(fullLockerUpdate, true, connection, transaction);
                    }
                    else
                    {
                        await daoLocker.UnassignLockersFromRentalTransactionAsync([lockerId], connection, transaction);
                    }

                    var daoRental = new DaoRental(_accessDB);
                    var daoClient = new DaoClient(_accessDB);
                    DataTable rentalDt = await daoRental.GetRentalById(existingRentalId.Value);
                    if (rentalDt.Rows.Count > 0)
                    {
                        int clientId = Convert.ToInt32(rentalDt.Rows[0]["client_id"]);
                        await daoClient.CloseLockerHistoryTransactionAsync(clientId, [lockerId], connection, transaction);
                    }

                    var remainingLockerIds = await daoLocker.GetLockerIdsByRentalIdTransactionAsync(existingRentalId.Value, connection, transaction);
                    decimal newContractedM3 = await daoLocker.CalculateTotalM3ForLockersAsync(remainingLockerIds, connection, transaction);
                    await daoRental.UpdateContractedM3TransactionAsync(existingRentalId.Value, newContractedM3, connection, transaction);

                    var cmbService = new GuardeSoftwareAPI.Services.clientMonthBalance.ClientMonthBalanceService(_accessDB);
                    await cmbService.RebuildForRentalTransactionAsync(existingRentalId.Value, connection, transaction);

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

        public async Task<List<int>> GetLockerIdsByRentalIdTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.");
            return await daoLocker.GetLockerIdsByRentalIdTransactionAsync(rentalId, connection, transaction);
        }

        public async Task<bool> UnassignLockersFromRentalTransactionAsync(List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
             if (lockerIds == null || !lockerIds.Any()) return true; // Nada que hacer
             // Validar IDs si es necesario
             int rowsAffected = await daoLocker.UnassignLockersFromRentalTransactionAsync(lockerIds, connection, transaction);
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