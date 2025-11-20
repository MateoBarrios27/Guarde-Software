using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Dtos.Statistics;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoStatistics
    {
        private readonly AccessDB accessDB;

        public DaoStatistics(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<MonthlyStatisticsDTO> GetMonthlyStatistics(int year, int month)
        {
            // Consulta optimizada para obtener todas las métricas en una sola llamada
            string query = @"
                DECLARE @StartDate DATE = DATEFROMPARTS(@Year, @Month, 1);
                DECLARE @EndDate DATE = EOMONTH(@StartDate);

                -- 1. Total Abono (Lo que ingresaría por alquileres):
                -- Suma del 'amount' vigente al día 1 del mes para todos los alquileres activos en esa fecha.
                -- Usamos una subconsulta para obtener el historial vigente al @StartDate.
                DECLARE @TotalAbono DECIMAL(18,2) = (
                    SELECT ISNULL(SUM(rh.amount), 0)
                    FROM rentals r
                    CROSS APPLY (
                        SELECT TOP 1 amount
                        FROM rental_amount_history h
                        WHERE h.rental_id = r.rental_id
                          AND h.start_date <= @StartDate
                        ORDER BY h.start_date DESC
                    ) rh
                    WHERE r.active = 1
                      AND r.start_date <= @StartDate
                      AND (r.end_date IS NULL OR r.end_date >= @StartDate)
                );

                SELECT
                    @TotalAbono AS TotalAbono,

                    -- 2. Total Cobrado: Suma de pagos realizados en el mes (créditos)
                    ISNULL(SUM(CASE 
                        WHEN movement_type = 'CREDITO' 
                             AND movement_date >= @StartDate 
                             AND movement_date <= DATEADD(day, 1, @EndDate) -- Incluir todo el último día
                        THEN amount ELSE 0 END), 0) AS TotalCobrado,

                    -- 3. Total Saldo: Sumatoria de los balances de todos los clientes al cierre del mes.
                    -- Balance = Débitos - Créditos acumulados hasta el final del mes.
                    ISNULL(SUM(CASE 
                        WHEN movement_type = 'DEBITO' AND movement_date <= DATEADD(day, 1, @EndDate) THEN amount 
                        WHEN movement_type = 'CREDITO' AND movement_date <= DATEADD(day, 1, @EndDate) THEN -amount 
                        ELSE 0 END), 0) AS TotalSaldo,

                    -- 4. Total Interés: Intereses generados en ese mes
                    ISNULL(SUM(CASE 
                        WHEN movement_type = 'DEBITO' 
                             AND concept LIKE 'Interés%' 
                             AND movement_date >= @StartDate 
                             AND movement_date <= DATEADD(day, 1, @EndDate)
                        THEN amount ELSE 0 END), 0) AS TotalInteres,

                    -- 5. Total Saldo Anterior: Deuda acumulada hasta el inicio del mes (antes del día 1)
                    ISNULL(SUM(CASE 
                        WHEN movement_type = 'DEBITO' AND movement_date < @StartDate THEN amount 
                        WHEN movement_type = 'CREDITO' AND movement_date < @StartDate THEN -amount 
                        ELSE 0 END), 0) AS TotalSaldoAnterior

                FROM account_movements;
            ";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@Year", SqlDbType.Int) { Value = year },
                new SqlParameter("@Month", SqlDbType.Int) { Value = month }
            };

            DataTable table = await accessDB.GetTableAsync("Statistics", query, parameters);

            if (table.Rows.Count > 0)
            {
                DataRow row = table.Rows[0];
                return new MonthlyStatisticsDTO
                {
                    Year = year,
                    Month = month,
                    TotalAbono = row["TotalAbono"] != DBNull.Value ? Convert.ToDecimal(row["TotalAbono"]) : 0,
                    TotalCobrado = row["TotalCobrado"] != DBNull.Value ? Convert.ToDecimal(row["TotalCobrado"]) : 0,
                    TotalSaldo = row["TotalSaldo"] != DBNull.Value ? Convert.ToDecimal(row["TotalSaldo"]) : 0,
                    TotalInteres = row["TotalInteres"] != DBNull.Value ? Convert.ToDecimal(row["TotalInteres"]) : 0,
                    TotalSaldoAnterior = row["TotalSaldoAnterior"] != DBNull.Value ? Convert.ToDecimal(row["TotalSaldoAnterior"]) : 0
                };
            }

            return new MonthlyStatisticsDTO { Year = year, Month = month };
        }
    }
}
