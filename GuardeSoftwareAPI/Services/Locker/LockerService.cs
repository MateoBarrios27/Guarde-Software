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

		public List<Locker> GetLockersList() {

			DataTable LockerTable = daoLocker.GetLockers();
			List<Locker> lockersList = new List<Locker>();

			foreach (DataRow row in LockerTable.Rows) {

				Locker locker = new Locker
				{
					Id = row.Field<int>("locker_id"),
                    WarehouseId = row.Field<int>("warehouse_id"),
                    LockerTypeId = row.Field<int>("locker_type_id"),
					Identifier = row["identifier"]?.ToString() ?? string.Empty,
                    Features =row["features"]?.ToString() ?? string.Empty,
                    Status = row["status"]?.ToString() ?? string.Empty,
                };	
				lockersList.Add(locker);
			}
			return lockersList;
		}

        public List<Locker> GetLockerListById(int id)
        {

            DataTable LockerTable = daoLocker.GetLockerById(id);
            List<Locker> lockersList = new List<Locker>();

            foreach (DataRow row in LockerTable.Rows)
            {

                Locker locker = new Locker
                {
                    Id = row.Field<int>("locker_id"),
                    WarehouseId = row.Field<int>("warehouse_id"),
                    LockerTypeId = row.Field<int>("locker_type_id"),
                    Identifier = row["identifier"]?.ToString() ?? string.Empty,
                    Features = row["features"]?.ToString() ?? string.Empty,
                    Status = row["status"]?.ToString() ?? string.Empty,
                };
                lockersList.Add(locker);
            }
            return lockersList;
        }

        public bool CreateLocker(Locker locker)
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

            if (daoLocker.CreateLocker(locker)) return true;
            else return false;
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

        public bool DeleteLocker(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid Locker Id.");

            if (daoLocker.DeleteLocker(id)) return true;
            else return false;
        }

        public async Task<bool> IsLockerAvailableAsync(int lockerId, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerId <= 0)
                throw new ArgumentException("Invalid locker ID.", nameof(lockerId));

            return await daoLocker.IsLockerIsAvailabeAsync(lockerId, connection, transaction);
        }


    }
}