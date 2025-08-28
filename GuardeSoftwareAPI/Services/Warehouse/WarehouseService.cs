using System;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Services.warehouse
{

	public class WarehouseService : IWarehouseService
    {
		readonly DaoWarehouse _daoWarehouse;
		public WarehouseService(AccessDB accessDB)
		{
			_daoWarehouse = new DaoWarehouse(accessDB);
		}

	}
}