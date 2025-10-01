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

		public LockerService(AccessDB accessDB)
		{
			daoLocker = new DaoLocker(accessDB);
		}

		public async Task<List<Locker>> GetLockersList() {

			DataTable LockerTable = await daoLocker.GetLockers();
			List<Locker> lockersList = new List<Locker>();

			foreach (DataRow row in LockerTable.Rows) {

				Locker locker = new Locker
				{
					Id = row.Field<int>("locker_id"),
                    WarehouseId = row.Field<int>("warehouse_id"),
                    LockerTypeId = row.Field<int>("locker_type_id"),
                    RentalId = row["rental_id"] != DBNull.Value ? (int?)row["rental_id"] : null,
					Identifier = row["identifier"]?.ToString() ?? string.Empty,
                    Features =row["features"]?.ToString() ?? string.Empty,
                    Status = row["status"]?.ToString() ?? string.Empty,
                };	
				lockersList.Add(locker);
			}
			return lockersList;
		}

        public async Task<List<Locker>> GetLockerListById(int id)
        {

            DataTable LockerTable = await daoLocker.GetLockerById(id);
            List<Locker> lockersList = new List<Locker>();

            foreach (DataRow row in LockerTable.Rows)
            {

                Locker locker = new Locker
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
                    LockerType = row["locker_type"]?.ToString() ?? string.Empty,
                    Identifier = row["identifier"]?.ToString() ?? string.Empty,
                    Warehouse = row["warehouse"]?.ToString() ?? string.Empty,
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

            if (string.IsNullOrWhiteSpace(dto.Features))
                throw new ArgumentException("Locker Features is required.");

            if (string.IsNullOrWhiteSpace(dto.Status))
                throw new ArgumentException("Locker Status is required.");

            var Locker = new Locker
            {
                Id = Id,
                Identifier = dto.Identifier,
                Features = dto.Features,
                Status = dto.Status,
            };

            return await daoLocker.UpdateLocker(Locker);  
        }

        public async Task<bool> UpdateLockerStatus(int lockerId, UpdateLockerStatusDto dto)
        {
            if (lockerId <= 0) throw new ArgumentException("Invalid locker ID.");
            if (string.IsNullOrWhiteSpace(dto.Status)) throw new ArgumentException("Status is required.");

            
            return await daoLocker.UpdateLockerStatus(lockerId, dto.Status);
        }


    }
}