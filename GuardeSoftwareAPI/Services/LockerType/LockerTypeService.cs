using System;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Services.lockerType
{

	public class LockerTypeService : ILockerTypeService
    {
		private readonly DaoLockerType daoLockerType;

		public LockerTypeService(AccessDB accessDB)
		{
			daoLockerType = new DaoLockerType(accessDB);
		}

		public List<LockerType> GetLockerTypesList()
		{
			DataTable lockerstypeTable = daoLockerType.GetLockerTypes();
			List<LockerType> lockerTypeList = new List<LockerType>();

			foreach (DataRow row in lockerstypeTable.Rows) {

				LockerType lockerType = new LockerType
				{
					Id = row.Field<int>("locker_type_id"),
                    Name = row["name"]?.ToString() ?? string.Empty,
                    Amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0m,
                    M3 = row["m3"] != DBNull.Value ? Convert.ToDecimal(row["m3"]) : 0m,
                };			
				lockerTypeList.Add(lockerType);
			}
			return lockerTypeList;
		}

        public List<LockerType> GetLockerTypeListById(int id)
        {
            DataTable lockerstypeTable = daoLockerType.GetLockerTypeById(id);
            List<LockerType> lockerTypeList = new List<LockerType>();

            foreach (DataRow row in lockerstypeTable.Rows)
            {

                LockerType lockerType = new LockerType
                {
                    Id = row.Field<int>("locker_type_id"),
                    Name = row["name"]?.ToString() ?? string.Empty,
                    Amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0m,
                    M3 = row["m3"] != DBNull.Value ? Convert.ToDecimal(row["m3"]) : 0m,
                };
                lockerTypeList.Add(lockerType);
            }
            return lockerTypeList;
        }
    }
}