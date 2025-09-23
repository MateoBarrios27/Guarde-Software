using System;
using System.Data;
using System.Collections.Generic;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Services.increaseRegimen
{
	

	public class IncreaseRegimenService : IIncreaseRegimenService
    {
        private readonly DaoIncreaseRegimen daoIncreaseRegimen;

        public IncreaseRegimenService(AccessDB accessDB)
		{
			daoIncreaseRegimen = new DaoIncreaseRegimen(accessDB);
		}
		
		public async Task<List<IncreaseRegimen>> GetIncreaseRegimensList()
		{
			DataTable increaseRegimensTable = await daoIncreaseRegimen.GetIncreaseRegimens();
			List<IncreaseRegimen> increaseRegimensList = new List<IncreaseRegimen>();

			foreach (DataRow row in increaseRegimensTable.Rows) {

				IncreaseRegimen increaseRegimen = new IncreaseRegimen
				{
					Id = row.Field<int>("regimen_id"),
                    Frequency = row.Field<int>("frequency"),
					Percentage = row["percentage"] != DBNull.Value ? Convert.ToDecimal(row["percentage"]) : 0m
                };
				increaseRegimensList.Add(increaseRegimen);
			}
			return increaseRegimensList;

		}

        public async Task<List<IncreaseRegimen>> GetIncreaseRegimenListById(int id)
        {
            DataTable increaseRegimensTable = await daoIncreaseRegimen.GetIncreaseRegimenById(id);
            List<IncreaseRegimen> increaseRegimensList = new List<IncreaseRegimen>();

            foreach (DataRow row in increaseRegimensTable.Rows)
            {

                IncreaseRegimen increaseRegimen = new IncreaseRegimen
                {
                    Id = row.Field<int>("regimen_id"),
                    Frequency = row.Field<int>("frequency"),
                    Percentage = row["percentage"] != DBNull.Value ? Convert.ToDecimal(row["percentage"]) : 0m
                };
                increaseRegimensList.Add(increaseRegimen);
            }
            return increaseRegimensList;

        }

        public async Task<bool> CreateIncreaseRegimen(IncreaseRegimen increaseRegimen)
        {
            if (increaseRegimen == null)
                throw new ArgumentNullException(nameof(increaseRegimen));

            if (increaseRegimen.Frequency <= 0)
                throw new ArgumentException("Invalid Frequency.");

            if (increaseRegimen.Percentage <= 0)
                throw new ArgumentException("Invalid Percentage.");

            if (await daoIncreaseRegimen.CreateIncreaseRegimen(increaseRegimen)) return true;
            else return false;
        }
    }
}