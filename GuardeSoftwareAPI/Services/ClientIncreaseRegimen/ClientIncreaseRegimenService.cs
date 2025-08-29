using System;
using System.Collections.Generic;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.clientIncreaseRegimen
{

	public class ClientIncreaseRegimenService : IClientIncreaseRegimenService
    {
		private readonly DaoClientIncreaseRegimen daoClientIncreaseRegimen;

		public ClientIncreaseRegimenService(AccessDB accessDB)
		{
			daoClientIncreaseRegimen = new DaoClientIncreaseRegimen(accessDB);
		}

		public List<ClientIncreaseRegimen> GetClientIncreaseRegimensList() {

			DataTable ClientIncreaseTable = daoClientIncreaseRegimen.GetClientIncreaseRegimens();
			List<ClientIncreaseRegimen> clientIncrease = new List<ClientIncreaseRegimen>();

			foreach (DataRow row in ClientIncreaseTable.Rows)
			{

				ClientIncreaseRegimen client = new ClientIncreaseRegimen
				{
					ClientId = row["client_id"] != DBNull.Value
                    ? (int)row["client_id"] : 0,

                    RegimenId = row["regimen_id"] != DBNull.Value
                    ? (int)row["regimen_id"] : 0,

                    StartDate = row.Field<DateTime>("start_date"),

                    EndDate = row.Field<DateTime?>("end_date"),
                };
				clientIncrease.Add(client);
			}
			return clientIncrease;
		}

        
        public List<ClientIncreaseRegimen> GetClientIncreaseRegimensListByClientId(int id)
        {

            DataTable ClientIncreaseTable = daoClientIncreaseRegimen.GetClientIncreaseRegimensByClientId(id);
            List<ClientIncreaseRegimen> clientIncrease = new List<ClientIncreaseRegimen>();

            foreach (DataRow row in ClientIncreaseTable.Rows)
            {

                ClientIncreaseRegimen client = new ClientIncreaseRegimen
                {
                    ClientId = row["client_id"] != DBNull.Value
                    ? (int)row["client_id"] : 0,

                    RegimenId = row["regimen_id"] != DBNull.Value
                    ? (int)row["regimen_id"] : 0,

                    StartDate = row.Field<DateTime>("start_date"),

                    EndDate = row.Field<DateTime?>("end_date"),
                };
                clientIncrease.Add(client);
            }
            return clientIncrease;
        }
    }
}