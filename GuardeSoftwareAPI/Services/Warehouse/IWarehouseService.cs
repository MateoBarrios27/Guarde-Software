using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.warehouse
{
	public interface IWarehouseService
	{
		public List<Warehouse> GetWarehouseList();
		public Warehouse GetWarehouseById(int id);
	}
}