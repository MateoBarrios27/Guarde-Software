using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.MonthlyIncrease;
using GuardeSoftwareAPI.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Services.monthlyIncrease
{
    public class MonthlyIncreaseService : IMonthlyIncreaseService
    {
        private readonly DaoMonthlyIncrease _daoMonthlyIncrease;
        private readonly ILogger<MonthlyIncreaseService> _logger;

        public MonthlyIncreaseService(AccessDB accessDB, ILogger<MonthlyIncreaseService> logger)
        {
            _daoMonthlyIncrease = new DaoMonthlyIncrease(accessDB);
            _logger = logger;
        }

        public async Task<List<MonthlyIncreaseSetting>> GetSettingsAsync()
        {
            var list = new List<MonthlyIncreaseSetting>();
            DataTable table = await _daoMonthlyIncrease.GetAllAsync();
            foreach (DataRow row in table.Rows)
            {
                list.Add(MapDataRowToSetting(row));
            }
            return list;
        }

        public async Task<MonthlyIncreaseSetting> CreateSettingAsync(CreateMonthlyIncreaseDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.EffectiveDate))
            {
                throw new ArgumentException("La fecha es requerida.");
            }
            if (dto.Percentage <= 0)
            {
                 throw new ArgumentException("El porcentaje debe ser mayor a 0.");
            }

            // Convertir "YYYY-MM" a DateTime
            if (!DateTime.TryParseExact(dto.EffectiveDate + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime effectiveDate))
            {
                throw new ArgumentException("Formato de fecha inválido. Se esperaba AAAA-MM.");
            }

            // Validar duplicados
            if (await _daoMonthlyIncrease.IsMonthAlreadyConfiguredAsync(effectiveDate))
            {
                throw new InvalidOperationException($"Ya existe una configuración de aumento para el mes {dto.EffectiveDate}. Edite la existente.");
            }

            int newId = await _daoMonthlyIncrease.CreateAsync(effectiveDate, dto.Percentage);
            
            DataRow? row = await _daoMonthlyIncrease.GetByIdAsync(newId);
            if (row == null)
            {
                throw new InvalidOperationException("No se pudo recuperar la configuración después de crearla.");
            }
            return MapDataRowToSetting(row);
        }

        public async Task<bool> UpdateSettingAsync(int id, UpdateMonthlyIncreaseDTO dto)
        {
            if (id <= 0) throw new ArgumentException("ID inválido.");
            if (dto.Percentage <= 0)
            {
                 throw new ArgumentException("El porcentaje debe ser mayor a 0.");
            }
            
            _logger.LogInformation($"Actualizando aumento ID {id} al {dto.Percentage}");
            return await _daoMonthlyIncrease.UpdateAsync(id, dto.Percentage);
        }

        public async Task<bool> DeleteSettingAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID inválido.");
            
            // Aquí no hay validación "en uso", ya que borrar un aumento
            // futuro simplemente significa que no se aplicará.
            _logger.LogInformation($"Eliminando aumento ID {id}.");
            return await _daoMonthlyIncrease.DeleteAsync(id);
        }

        private MonthlyIncreaseSetting MapDataRowToSetting(DataRow row)
        {
            return new MonthlyIncreaseSetting
            {
                Id = Convert.ToInt32(row["increase_setting_id"]),
                EffectiveDate = Convert.ToDateTime(row["effective_date"]),
                Percentage = Convert.ToDecimal(row["percentage"]),
                CreatedAt = Convert.ToDateTime(row["created_at"]),
            };
        }
    }
}