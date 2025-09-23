using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.warehouse
{
	public interface IWarehouseService
	{
		Task<List<Warehouse>> GetWarehouseList();
		Task<Warehouse> GetWarehouseById(int id);
		Task<bool> CreateWarehouse(Warehouse warehouse);
		Task<bool> DeleteWarehouse(int warehouseId);
	}
}