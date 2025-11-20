using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Statistics;

namespace GuardeSoftwareAPI.Services.statistics
{
    public class StatisticsService : IStatisticsService
    {
        private readonly DaoStatistics _daoStatistics;
        private readonly ILogger<StatisticsService> _logger;

        public StatisticsService(AccessDB accessDB, ILogger<StatisticsService> logger)
        {
            _daoStatistics = new DaoStatistics(accessDB);
            _logger = logger;
        }

        public async Task<MonthlyStatisticsDTO> GetMonthlyStatistics(int year, int month)
        {
            try
            {
                return await _daoStatistics.GetMonthlyStatistics(year, month);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo estadísticas para {month}/{year}");
                throw;
            }
        }
    }
}
