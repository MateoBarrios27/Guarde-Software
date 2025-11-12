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
            // El Job corre el día 1ro del mes.
            var today = DateTime.Now.Date; 

            // 1. Obtener alquileres cuya fecha ancla es HOY o anterior, y no estén congelados.
            DataTable rentalsToIncrease = await _daoRental.GetRentalsDueForIncreaseTodayAsync(today);
            _logger.LogInformation($"Se encontraron {rentalsToIncrease.Rows.Count} alquileres que deben ser revisados para aumento.");

            foreach (DataRow row in rentalsToIncrease.Rows)
            {
                int rentalId = Convert.ToInt32(row["rental_id"]);
                int frequency = Convert.ToInt32(row["increase_frequency_months"]);
                DateTime anchorDate = Convert.ToDateTime(row["increase_anchor_date"]); // Fecha ancla (ej: 01/11/2025)
                decimal currentAmount = Convert.ToDecimal(row["CurrentAmount"]);
                int lastHistoryId = Convert.ToInt32(row["LastHistoryId"]);
                DateTime lastIncreaseDate = Convert.ToDateTime(row["LastIncreaseDate"]); // Fecha del monto actual (ej: 01/07/2025)
                DateTime? priceLockDate = row["price_lock_end_date"] as DateTime?;

                // 2. Determinar la fecha de inicio para el "catch-up"
                DateTime catchUpStartDate = anchorDate;
                if (priceLockDate.HasValue && priceLockDate.Value > catchUpStartDate)
                {
                    // Si el bloqueo de precio terminó DESPUÉS del ancla, empezamos a contar desde ahí.
                    catchUpStartDate = priceLockDate.Value;
                }
                
                // Si la fecha de "puesta al día" sigue siendo en el futuro, omitir.
                // (Esto no debería pasar si la query GetRentalsDueForIncreaseTodayAsync es correcta)
                if (catchUpStartDate > today)
                {
                    continue;
                }

                _logger.LogInformation($"Procesando Rental ID {rentalId}. Monto actual: {currentAmount:C}. Frecuencia: {frequency}m. Fecha ancla: {anchorDate:d}. Fecha inicio catch-up: {catchUpStartDate:d}.");

                decimal newAmount = currentAmount;
                DateTime newAnchorDate = anchorDate; // La próxima fecha ancla a guardar
                DateTime lastAppliedIncreaseDate = lastIncreaseDate; // La fecha de inicio del nuevo monto

                using (var connection = _accessDB.GetConnectionClose())
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 3. Obtener TODOS los porcentajes definidos entre la fecha de inicio del catch-up y hoy
                            // (Usamos AddDays(-1) para incluir el mes de catchUpStartDate si también es un aniversario)
                            Dictionary<DateTime, decimal> increasesMap = await _daoMonthlyIncrease.GetApplicableIncreasesBetweenDatesAsync(catchUpStartDate.AddDays(-1), today, connection, transaction);

                            if (!increasesMap.Any(kv => kv.Key >= newAnchorDate))
                            {
                                _logger.LogWarning($"No se encontraron porcentajes de aumento definidos en o después de {newAnchorDate:d} para Rental ID {rentalId}.");
                                // Movemos la fecha ancla al futuro para no revisar todos los días.
                                newAnchorDate = today.AddMonths(frequency); 
                                await _daoRental.UpdateNextIncreaseDateTransactionAsync(rentalId, newAnchorDate, connection, transaction);
                                await transaction.CommitAsync();
                                _logger.LogInformation($"Fecha ancla de Rental ID {rentalId} movida a {newAnchorDate:d} sin cambios de precio.");
                                continue;
                            }

                            // 4. Bucle de "Puesta al Día"
                            while (newAnchorDate <= today)
                            {
                                // Buscamos el aumento definido para el mes de ESTA fecha ancla
                                var settingDate = new DateTime(newAnchorDate.Year, newAnchorDate.Month, 1);
                                
                                if (increasesMap.TryGetValue(settingDate, out decimal percentage))
                                {
                                    // ¡Encontramos un aumento que se perdió!
                                    newAmount = newAmount * (1 + (percentage / 100));
                                    _logger.LogInformation($"  -> Aplicando aumento de {percentage}% (para {settingDate:MM/yyyy}) a Rental ID {rentalId}. Monto intermedio: {newAmount:C}");
                                    
                                    // Guardamos esta fecha, será la 'start_date' del nuevo monto
                                    lastAppliedIncreaseDate = newAnchorDate; 
                                } 
                                else
                                {
                                     _logger.LogWarning($"  -> OMITIENDO aumento para {settingDate:MM/yyyy} (Rental ID {rentalId}) - No se definió porcentaje global para ese mes.");
                                }

                                // Calculamos el *próximo* ancla
                                newAnchorDate = newAnchorDate.AddMonths(frequency);
                            }

                            // 5. Redondear y aplicar el cambio
                            decimal finalAmount = RoundUpToNearest100(newAmount);

                            if (finalAmount > currentAmount)
                            {
                                _logger.LogInformation($"Monto final para Rental ID {rentalId}: {finalAmount:C} (redondeado desde {newAmount:C}). Actualizando BD.");
                                
                                // 5a. Cerrar historial viejo y crear el nuevo
                                await _rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(
                                    lastHistoryId,
                                    rentalId,
                                    finalAmount,
                                    lastAppliedIncreaseDate, // <-- CORRECCIÓN: Usar la fecha del último aumento aplicado
                                    connection,
                                    transaction
                                );

                                // 5b. Actualizar la fecha ancla en RENTALS a la próxima fecha futura
                                await _daoRental.UpdateNextIncreaseDateTransactionAsync(rentalId, newAnchorDate, connection, transaction);
                                
                                await transaction.CommitAsync();
                            }
                            else
                            {
                                _logger.LogInformation($"Sin cambios de monto para Rental ID {rentalId} (Monto calculado: {finalAmount:C}).");
                                // Si no hubo aumento (ej. % 0) pero la fecha ancla está vencida, igual la actualizamos
                                if(newAnchorDate > anchorDate)
                                {
                                     await _daoRental.UpdateNextIncreaseDateTransactionAsync(rentalId, newAnchorDate, connection, transaction);
                                     await transaction.CommitAsync();
                                     _logger.LogInformation($"Fecha ancla de Rental ID {rentalId} movida a {newAnchorDate:d} sin cambios de precio.");
                                } else {
                                     await transaction.RollbackAsync(); // No hubo cambios
                                }
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

        /// <summary>
        /// Redondea un monto hacia ARRIBA a los 100 más cercanos.
        /// Ej: 141,420 -> 141,500. Ej: 141,501 -> 141,600
        /// </summary>
        private decimal RoundUpToNearest100(decimal amount)
        {
            if (amount == 0) return 0;
            // Usamos Ceiling para redondear siempre hacia arriba
            return Math.Ceiling(amount / 100.0m) * 100;
        }
    }
}