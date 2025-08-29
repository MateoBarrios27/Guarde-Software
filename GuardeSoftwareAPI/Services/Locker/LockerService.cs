using System;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;

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
    }
}