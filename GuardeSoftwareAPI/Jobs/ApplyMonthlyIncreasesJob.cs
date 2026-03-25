using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using Quartz;
using System.Data;

namespace GuardeSoftwareAPI.Jobs
{
    [DisallowConcurrentExecution]
    public class ApplyMonthlyIncreasesJob : IJob
    {
        private readonly ILogger<ApplyMonthlyIncreasesJob> _logger;
        private readonly AccessDB _accessDB;
        private readonly DaoRental _daoRental;
        private readonly DaoRentalAmountHistory _daoRentalAmountHistory;
        private readonly DaoMonthlyIncrease _daoMonthlyIncrease;
        private readonly IRentalAmountHistoryService _rentalAmountHistoryService;

        public ApplyMonthlyIncreasesJob(ILogger<ApplyMonthlyIncreasesJob> logger, AccessDB accessDB, IRentalAmountHistoryService rentalAmountHistoryService)
        {
            _logger = logger;
            _accessDB = accessDB;
            _daoRental = new DaoRental(_accessDB);
            _daoRentalAmountHistory = new DaoRentalAmountHistory(_accessDB);
            _daoMonthlyIncrease = new DaoMonthlyIncrease(_accessDB);
            _rentalAmountHistoryService = rentalAmountHistoryService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("--- Iniciando Job de Aplicación de Aumentos Mensuales ---");
            var today = DateTime.Now.Date; 

            DataTable rentalsToIncrease = await _daoRental.GetRentalsDueForIncreaseTodayAsync(today);
            _logger.LogInformation($"Se encontraron {rentalsToIncrease.Rows.Count} alquileres que deben ser revisados para aumento.");

            foreach (DataRow row in rentalsToIncrease.Rows)
            {
                int rentalId = Convert.ToInt32(row["rental_id"]);
                int frequency = Convert.ToInt32(row["increase_frequency_months"]);
                DateTime anchorDate = Convert.ToDateTime(row["increase_anchor_date"]);
                decimal currentAmount = Convert.ToDecimal(row["CurrentAmount"]);
                int lastHistoryId = Convert.ToInt32(row["LastHistoryId"]);
                DateTime lastIncreaseDate = Convert.ToDateTime(row["LastIncreaseDate"]);
                DateTime? priceLockDate = row["price_lock_end_date"] as DateTime?;

                DateTime catchUpStartDate = anchorDate;
                if (priceLockDate.HasValue && priceLockDate.Value > catchUpStartDate)
                {
                    catchUpStartDate = priceLockDate.Value;
                }
                
                if (catchUpStartDate > today)
                {
                    continue;
                }

                _logger.LogInformation($"Procesando Rental ID {rentalId}. Monto actual: {currentAmount:C}. Frecuencia: {frequency}m. Fecha ancla: {anchorDate:d}. Fecha inicio catch-up: {catchUpStartDate:d}.");

                decimal newAmount = currentAmount;
                DateTime newAnchorDate = anchorDate;
                DateTime lastAppliedIncreaseDate = lastIncreaseDate;

                using (var connection = _accessDB.GetConnectionClose())
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            Dictionary<DateTime, decimal> increasesMap = await _daoMonthlyIncrease.GetApplicableIncreasesBetweenDatesAsync(catchUpStartDate.AddDays(-1), today, connection, transaction);

                            if (!increasesMap.Any(kv => kv.Key >= newAnchorDate))
                            {
                                _logger.LogWarning($"No se encontraron porcentajes de aumento definidos en o después de {newAnchorDate:d} para Rental ID {rentalId}.");
                                newAnchorDate = today.AddMonths(frequency); 
                                await _daoRental.UpdateNextIncreaseDateTransactionAsync(rentalId, newAnchorDate, connection, transaction);
                                await transaction.CommitAsync();
                                _logger.LogInformation($"Fecha ancla de Rental ID {rentalId} movida a {newAnchorDate:d} sin cambios de precio.");
                                continue;
                            }

                            while (newAnchorDate <= today)
                            {
                                var settingDate = new DateTime(newAnchorDate.Year, newAnchorDate.Month, 1);
                                
                                if (increasesMap.TryGetValue(settingDate, out decimal percentage))
                                {
                                    newAmount = newAmount * (1 + (percentage / 100));
                                    _logger.LogInformation($"  -> Aplicando aumento de {percentage}% (para {settingDate:MM/yyyy}) a Rental ID {rentalId}. Monto intermedio: {newAmount:C}");
                                    
                                    lastAppliedIncreaseDate = newAnchorDate; 
                                } 
                                else
                                {
                                     _logger.LogWarning($"  -> OMITIENDO aumento para {settingDate:MM/yyyy} (Rental ID {rentalId}) - No se definió porcentaje global para ese mes.");
                                }

                                newAnchorDate = newAnchorDate.AddMonths(frequency);
                            }

                            decimal finalAmount = RoundToNearest1000(newAmount);

                            if (finalAmount > currentAmount)
                            {
                                _logger.LogInformation($"Monto final para Rental ID {rentalId}: {finalAmount:C} (redondeado desde {newAmount:C}). Actualizando BD.");
                                
                                await _rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(
                                    lastHistoryId,
                                    rentalId,
                                    finalAmount,
                                    lastAppliedIncreaseDate, 
                                    connection,
                                    transaction
                                );

                                await _daoRental.UpdateNextIncreaseDateTransactionAsync(rentalId, newAnchorDate, connection, transaction);
                                
                                await transaction.CommitAsync();
                            }
                            else
                            {
                                _logger.LogInformation($"Sin cambios de monto para Rental ID {rentalId} (Monto calculado: {finalAmount:C}).");
                                if(newAnchorDate > anchorDate)
                                {
                                     await _daoRental.UpdateNextIncreaseDateTransactionAsync(rentalId, newAnchorDate, connection, transaction);
                                     await transaction.CommitAsync();
                                     _logger.LogInformation($"Fecha ancla de Rental ID {rentalId} movida a {newAnchorDate:d} sin cambios de precio.");
                                } else {
                                     await transaction.RollbackAsync();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error al aplicar aumento para Rental ID {rentalId}. Transacción revertida.");
                            await transaction.RollbackAsync();
                        }
                    }
                }
            }
            _logger.LogInformation("--- Job de Aplicación de Aumentos finalizado ---");
        }

        private decimal RoundToNearest1000(decimal amount)
        {
            if (amount == 0) return 0;

            // Math.Round with MidpointRounding.AwayFromZero rounds .5 up to the next integer, which is what we want for our rounding logic.
            // Ej: 12500 / 1000 = 12.5 -> Round is 13 -> 13 * 1000 = 13000
            // Ej: 12499 / 1000 = 12.499 -> Round is 12 -> 12 * 1000 = 12000
            return Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
        }
    }
}