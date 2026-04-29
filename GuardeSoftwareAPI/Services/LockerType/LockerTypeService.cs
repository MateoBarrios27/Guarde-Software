using System;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Services.lockerType
{

    public class LockerTypeService : ILockerTypeService
    {
        private readonly DaoLockerType daoLockerType;

        public LockerTypeService(AccessDB accessDB)
        {
            daoLockerType = new DaoLockerType(accessDB);
        }

        public async Task<List<LockerType>> GetLockerTypesList()
        {
            DataTable lockerstypeTable = await daoLockerType.GetLockerTypes();
            List<LockerType> lockerTypeList = new List<LockerType>();

            foreach (DataRow row in lockerstypeTable.Rows)
            {

                LockerType lockerType = new LockerType
                {
                    Id = row.Field<int>("locker_type_id"),
                    Name = row["name"]?.ToString() ?? string.Empty,
                    M3 = row["m3"] != DBNull.Value ? Convert.ToDecimal(row["m3"]) : 0m,
                };
                lockerTypeList.Add(lockerType);
            }
            return lockerTypeList;
        }

        public async Task<List<LockerType>> GetLockerTypeListById(int id)
        {
            DataTable lockerstypeTable = await daoLockerType.GetLockerTypeById(id);
            List<LockerType> lockerTypeList = new List<LockerType>();

            foreach (DataRow row in lockerstypeTable.Rows)
            {

                LockerType lockerType = new()
                {
                    Id = row.Field<int>("locker_type_id"),
                    Name = row["name"]?.ToString() ?? string.Empty,
                    M3 = row["m3"] != DBNull.Value ? Convert.ToDecimal(row["m3"]) : 0m,
                };
                lockerTypeList.Add(lockerType);
            }
            return lockerTypeList;
        }

        public async Task<LockerType> CreateLockerType(LockerType lockerType)
        {
            if (lockerType == null)
                throw new ArgumentNullException(nameof(lockerType));

            if (string.IsNullOrWhiteSpace(lockerType.Name))
                throw new ArgumentException("Locker type name is required.");

            if (lockerType.M3 != null && lockerType.M3 <= 0)
                throw new ArgumentException("M3 must be greater than 0 if provided.");
                
            if (await daoLockerType.CheckIfLockerTypeNameExistsAsync(lockerType.Name))
                throw new ArgumentException("Locker type name already exists.");

            return await daoLockerType.CreateLockerType(lockerType);
        }

        public async Task<bool> UpdateLockerType(LockerType lockerType)
        {
            ArgumentNullException.ThrowIfNull(lockerType);

            if (lockerType.Id <= 0)
                throw new ArgumentException("Invalid locker type ID.");

            if (string.IsNullOrWhiteSpace(lockerType.Name))
                throw new ArgumentException("Locker type name is required.");

            if (lockerType.M3 != null && lockerType.M3 <= 0)
                throw new ArgumentException("M3 must be greater than 0 if provided.");
                
            if (await daoLockerType.CheckIfLockerTypeNameExistsAsync(lockerType.Name, lockerType.Id))
                throw new ArgumentException("Locker type name already exists.");

            return await daoLockerType.UpdateLockerType(lockerType);
        }

        public async Task<bool> DeleteLockerType(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid locker type ID.");

            return await daoLockerType.DeleteLockerType(id);
        }
    }
}