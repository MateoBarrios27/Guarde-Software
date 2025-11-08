using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.BillingType;
using GuardeSoftwareAPI.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Services.billingType
{
    public class BillingTypeService : IBillingTypeService
    {
        private readonly DaoBillingType _daoBillingType;
        private readonly ILogger<BillingTypeService> _logger;

        public BillingTypeService(AccessDB accessDB, ILogger<BillingTypeService> logger)
        {
            _daoBillingType = new DaoBillingType(accessDB);
            _logger = logger;
        }

        public async Task<List<BillingType>> GetBillingTypesAsync()
        {
            var list = new List<BillingType>();
            DataTable table = await _daoBillingType.GetBillingTypesAsync();
            foreach (DataRow row in table.Rows)
            {
                list.Add(MapDataRowToBillingType(row));
            }
            return list;
        }

        public async Task<BillingType> CreateBillingTypeAsync(CreateBillingTypeDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            {
                throw new ArgumentException("El nombre es requerido.");
            }

            int newId = await _daoBillingType.CreateBillingTypeAsync(dto.Name.Trim());
            
            DataRow? row = await _daoBillingType.GetBillingTypeByIdAsync(newId);
            if (row == null)
            {
                throw new InvalidOperationException("No se pudo recuperar el tipo de factura después de crearlo.");
            }
            return MapDataRowToBillingType(row);
        }

        public async Task<bool> UpdateBillingTypeAsync(int id, UpdateBillingTypeDTO dto)
        {
            if (id <= 0) throw new ArgumentException("ID inválido.");
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            {
                throw new ArgumentException("El nombre es requerido.");
            }
            return await _daoBillingType.UpdateBillingTypeAsync(id, dto.Name.Trim());
        }

        public async Task<bool> DeleteBillingTypeAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID inválido.");

            // Lógica de negocio: No permitir borrar si está en uso
            if (await _daoBillingType.IsBillingTypeInUseAsync(id))
            {
                _logger.LogWarning("Intento de eliminar tipo de factura ID {Id} que está en uso.", id);
                throw new InvalidOperationException("Este tipo de factura no se puede eliminar porque está siendo utilizado por uno o más clientes.");
            }

            return await _daoBillingType.DeleteBillingTypeAsync(id);
        }

        private BillingType MapDataRowToBillingType(DataRow row)
        {
            return new BillingType
            {
                Id = Convert.ToInt32(row["billing_type_id"]),
                Name = row["name"].ToString() ?? "",
                Active = Convert.ToBoolean(row["active"])
            };
        }
    }
}