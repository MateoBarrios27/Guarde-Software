using System;
using System.Data;
using GuardeSoftwareAPI.Dao;
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

		public List<Warehouse> GetWarehouseList()
		{
			DataTable warehouseTable = _daoWarehouse.GetWarehouses();
			List<Warehouse> warehouses = new List<Warehouse>();

			if (warehouseTable.Rows.Count == 0) throw new ArgumentException("No warehouse found.");

			foreach (DataRow row in warehouseTable.Rows)
			{
				int warehouseId = (int)row["warehouse_id"];

				Warehouse warehouse = new Warehouse
				{
					Id = warehouseId,
					Name = row["name"]?.ToString() ?? string.Empty,
					Address = row["address"]?.ToString() ?? string.Empty
				};

				warehouses.Add(warehouse);
			}

			return warehouses;
		}

		public Warehouse GetWarehouseById(int warehouseId)
		{
			if (warehouseId <= 0) throw new ArgumentException("Invalid warehouse ID.");

			DataTable warehouseTable = _daoWarehouse.GetWarehouseById(warehouseId);

			if (warehouseTable.Rows.Count == 0) throw new ArgumentException("No warehouse found with the given ID.");

			DataRow row = warehouseTable.Rows[0];

			return new Warehouse
			{
				Id = (int)row["warehouse_id"],
				Name = row["name"]?.ToString() ?? string.Empty,
				Address = row["address"]?.ToString() ?? string.Empty
			};
		}
		
		public bool CreateWarehouse(Warehouse warehouse)
		{
			if (warehouse == null) throw new ArgumentNullException(nameof(warehouse), "Warehouse cannot be null.");
			if (string.IsNullOrWhiteSpace(warehouse.Name)) throw new ArgumentException("Warehouse name cannot be empty.");
			if (string.IsNullOrWhiteSpace(warehouse.Address)) throw new ArgumentException("Warehouse address cannot be empty.");
			if (_daoWarehouse.CreateWarehouse(warehouse)) return true;
			else return false;
		}

		public bool DeleteWarehouse(int warehouseId)
		{
			if (warehouseId <= 0) throw new ArgumentException("Invalid warehouse ID.");
			if (_daoWarehouse.DeleteWarehouse(warehouseId)) return true;
			else return false;
		}
	}
}