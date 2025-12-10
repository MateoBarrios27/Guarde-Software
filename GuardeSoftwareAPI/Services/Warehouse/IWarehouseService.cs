using System;
using GuardeSoftwareAPI.Dtos.Warehouse;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.warehouse
{
	public interface IWarehouseService
	{
		Task<List<Warehouse>> GetWarehouseList();
		Task<Warehouse> GetWarehouseById(int id);
		Task<Warehouse> CreateWarehouseAsync(CreateWarehouseDTO dto);
        Task<bool> UpdateWarehouseAsync(int id, UpdateWarehouseDTO dto);
        Task<bool> DeleteWarehouseAsync(int id);
	}
}