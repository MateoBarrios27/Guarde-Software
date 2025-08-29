using System;
using System.Data;
using System.Collections.Generic;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Services.increaseRegimen
{
	

	public class IncreaseRegimenService : IIncreaseRegimenService
    {
        private readonly DaoIncreaseRegimen daoIncreaseRegimen;

        public IncreaseRegimenService(AccessDB accessDB)
		{
			daoIncreaseRegimen = new DaoIncreaseRegimen(accessDB);
		}
		
		public List<IncreaseRegimen> GetIncreaseRegimensList()
		{
			DataTable increaseRegimensTable = daoIncreaseRegimen.GetIncreaseRegimens();
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

        public List<IncreaseRegimen> GetIncreaseRegimenListById(int id)
        {
            DataTable increaseRegimensTable = daoIncreaseRegimen.GetIncreaseRegimenById(id);
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
    }
}