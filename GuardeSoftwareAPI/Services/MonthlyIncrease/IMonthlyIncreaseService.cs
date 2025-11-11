using GuardeSoftwareAPI.Dtos.MonthlyIncrease;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.monthlyIncrease
{
    public interface IMonthlyIncreaseService
    {
        Task<List<MonthlyIncreaseSetting>> GetSettingsAsync();
        Task<MonthlyIncreaseSetting> CreateSettingAsync(CreateMonthlyIncreaseDTO dto);
        Task<bool> UpdateSettingAsync(int id, UpdateMonthlyIncreaseDTO dto);
        Task<bool> DeleteSettingAsync(int id);
    }
}