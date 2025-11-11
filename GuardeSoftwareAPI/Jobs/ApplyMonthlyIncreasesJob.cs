using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq; // Para OrderBy
using System.Threading.Tasks;

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

            // 1. Obtener TODOS los alquileres que deben ser revisados
            DataTable rentalsToIncrease = await _daoRental.GetRentalsDueForIncreaseTodayAsync(today);
            _logger.LogInformation($"Se encontraron {rentalsToIncrease.Rows.Count} alquileres cuya fecha ancla es hoy o anterior.");

            foreach (DataRow row in rentalsToIncrease.Rows)
            {
                int rentalId = Convert.ToInt32(row["rental_id"]);
                int frequency = Convert.ToInt32(row["increase_frequency_months"]);
                DateTime anchorDate = Convert.ToDateTime(row["increase_anchor_date"]); // La fecha ancla (próximo aumento)
                decimal currentAmount = Convert.ToDecimal(row["CurrentAmount"]);
                int lastHistoryId = Convert.ToInt32(row["LastHistoryId"]);
                DateTime? priceLockDate = row["price_lock_end_date"] as DateTime?;

                // 2. Verificar bloqueo de precio (segunda validación)
                if (priceLockDate.HasValue && today < priceLockDate.Value)
                {
                    _logger.LogInformation($"OMITIENDO Aumento (Job): Rental ID {rentalId}. Tiene precio congelado hasta {priceLockDate.Value:yyyy-MM-dd}.");
                    continue;
                }

                // 3. Determinar la fecha de inicio para el "catch-up"
                // Es la fecha ancla (anchorDate) o la fecha de fin de bloqueo, la que sea MÁS RECIENTE.
                DateTime catchUpStartDate = anchorDate;
                if (priceLockDate.HasValue && priceLockDate.Value > catchUpStartDate)
                {
                    catchUpStartDate = priceLockDate.Value;
                }
                
                // Si la fecha de "puesta al día" sigue siendo en el futuro, omitir
                if (catchUpStartDate > today)
                {
                    continue;
                }

                _logger.LogInformation($"Procesando Rental ID {rentalId}. Monto actual: {currentAmount:C}. Frecuencia: {frequency}m. Fecha ancla: {anchorDate:d}. Fecha inicio catch-up: {catchUpStartDate:d}.");

                decimal newAmount = currentAmount;
                DateTime newAnchorDate = anchorDate;

                // 4. Iniciar bucle de "Puesta al Día"
                DateTime nextDateInLoop = anchorDate; 

                using (var connection = _accessDB.GetConnectionClose())
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 5. Obtener TODOS los porcentajes definidos entre la fecha de inicio del catch-up y hoy
                            Dictionary<DateTime, decimal> increasesMap = await _daoMonthlyIncrease.GetApplicableIncreasesBetweenDatesAsync(catchUpStartDate.AddDays(-1), today, connection, transaction);
                            
                            if (!increasesMap.Any())
                            {
                                _logger.LogWarning($"No se encontraron porcentajes de aumento definidos entre {catchUpStartDate:d} y {today:d} para Rental ID {rentalId}.");
                                // Si la fecha ancla está vencida pero no hay aumentos, la movemos.
                                newAnchorDate = nextDateInLoop;
                                while(newAnchorDate <= today) // Moverla al futuro
                                {
                                    newAnchorDate = newAnchorDate.AddMonths(frequency);
                                }
                                
                                await _daoRental.UpdateNextIncreaseDateTransactionAsync(rentalId, newAnchorDate, connection, transaction);
                                await transaction.CommitAsync();
                                _logger.LogInformation($"Fecha ancla de Rental ID {rentalId} movida a {newAnchorDate:d} sin cambios de precio.");
                                continue; // Siguiente cliente
                            }

                            // 6. Aplicar aumentos en orden
                            foreach (var increase in increasesMap.OrderBy(kv => kv.Key)) // Ordenar por fecha
                            {
                                DateTime increaseEffectiveDate = increase.Key; // Ej: 2025-01-01
                                decimal percentage = increase.Value; // Ej: 25.0

                                if (increaseEffectiveDate >= nextDateInLoop)
                                {
                                    newAmount = newAmount * (1 + (percentage / 100));
                                    _logger.LogInformation($"  -> Aplicando aumento de {percentage}% (para {increaseEffectiveDate:MM/yyyy}) a Rental ID {rentalId}. Monto intermedio: {newAmount:C}");
                                    
                                    nextDateInLoop = nextDateInLoop.AddMonths(frequency);
                                }
                            }

                            // 7. Redondear y aplicar el cambio
                            decimal finalAmount = RoundUpToNearest100(newAmount);

                            if (finalAmount > currentAmount)
                            {
                                _logger.LogInformation($"Monto final para Rental ID {rentalId}: {finalAmount:C} (redondeado desde {newAmount:C}). Actualizando BD.");
                                
                                // 7a. Cerrar historial viejo y crear el nuevo
                                await _rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(
                                    lastHistoryId,
                                    rentalId,
                                    finalAmount,
                                    today, // El nuevo monto rige desde HOY
                                    connection,
                                    transaction
                                );

                                // 7b. Calcular la SIGUIENTE fecha ancla
                                // Mover la fecha ancla al futuro
                                while(nextDateInLoop <= today)
                                {
                                    nextDateInLoop = nextDateInLoop.AddMonths(frequency);
                                }
                                await _daoRental.UpdateNextIncreaseDateTransactionAsync(rentalId, nextDateInLoop, connection, transaction);
                                
                                await transaction.CommitAsync();
                            }
                            else
                            {
                                _logger.LogInformation($"Sin cambios de monto para Rental ID {rentalId} (Monto calculado: {finalAmount:C}).");
                                await transaction.RollbackAsync(); // No hubo cambios
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error al aplicar aumento para Rental ID {rentalId}. Transacción revertida.");
                            await transaction.RollbackAsync();
                        }
                    } // Fin transaction
                } // Fin connection
            } // Fin foreach
            _logger.LogInformation("--- Job de Aplicación de Aumentos finalizado ---");
        }

        private decimal RoundUpToNearest100(decimal amount)
        {
            return Math.Ceiling(amount / 100.0m) * 100;
        }
    }
}