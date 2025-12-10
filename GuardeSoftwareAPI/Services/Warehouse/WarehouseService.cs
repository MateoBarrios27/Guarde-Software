using System;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Warehouse;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.warehouse
{

	public class WarehouseService : IWarehouseService
	{
		readonly DaoWarehouse _daoWarehouse;
		public WarehouseService(AccessDB accessDB)
		{
			_daoWarehouse = new DaoWarehouse(accessDB);
		}

		public async Task<List<Warehouse>> GetWarehouseList()
		{
			DataTable warehouseTable = await _daoWarehouse.GetWarehouses();
			List<Warehouse> warehouses = [];

			if (warehouseTable.Rows.Count == 0) throw new ArgumentException("No warehouse found.");

			foreach (DataRow row in warehouseTable.Rows)
			{
				int warehouseId = (int)row["warehouse_id"];

				Warehouse warehouse = new()
                {
					Id = warehouseId,
					Name = row["name"]?.ToString() ?? string.Empty,
					Address = row["address"]?.ToString() ?? string.Empty
				};

				warehouses.Add(warehouse);
			}

			return warehouses;
		}

		public async Task<Warehouse> GetWarehouseById(int warehouseId)
		{
			if (warehouseId <= 0) throw new ArgumentException("Invalid warehouse ID.");

			DataTable warehouseTable = await _daoWarehouse.GetWarehouseById(warehouseId);

			if (warehouseTable.Rows.Count == 0) throw new ArgumentException("No warehouse found with the given ID.");

			DataRow row = warehouseTable.Rows[0];

			return new Warehouse
			{
				Id = (int)row["warehouse_id"],
				Name = row["name"]?.ToString() ?? string.Empty,
				Address = row["address"]?.ToString() ?? string.Empty
			};
		}

		public async Task<Warehouse> CreateWarehouseAsync(CreateWarehouseDTO dto)
        {
            int id = await _daoWarehouse.CreateWarehouseAsync(dto.Name, dto.Address);
            return new Warehouse { Id = id, Name = dto.Name, Address = dto.Address };
        }

        public async Task<bool> UpdateWarehouseAsync(int id, UpdateWarehouseDTO dto)
        {
            return await _daoWarehouse.UpdateWarehouseAsync(id, dto.Name, dto.Address);
        }

        public async Task<bool> DeleteWarehouseAsync(int id)
        {
            // Validate if the warehouse has active lockers
            if (await _daoWarehouse.HasActiveLockersAsync(id))
            {
                throw new InvalidOperationException("No se puede eliminar el depósito porque tiene lockers asignados.");
            }
            return await _daoWarehouse.DeleteWarehouseAsync(id);
        }
	}
}