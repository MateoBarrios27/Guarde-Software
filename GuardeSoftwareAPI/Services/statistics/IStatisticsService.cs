using GuardeSoftwareAPI.Dtos.Statistics;

namespace GuardeSoftwareAPI.Services.statistics
{
    public interface IStatisticsService
    {
        Task<MonthlyStatisticsDTO> GetMonthlyStatistics(int year, int month);
        Task<ClientStatisticsDto> GetClientStatisticsAsync();
    }
}
