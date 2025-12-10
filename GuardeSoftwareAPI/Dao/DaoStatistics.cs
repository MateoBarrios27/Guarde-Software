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

        public async Task<MonthlyStatisticsDTO> GetMonthlyStatisticsAsync(int year, int month)
        {
            DateTime startDate = new(year, month, 1);
            DateTime endDate = startDate.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

            string query = @"
                DECLARE @Pagado DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(amount), 0) 
                    FROM account_movements 
                    WHERE movement_type = 'CREDITO' 
                      AND movement_date BETWEEN @StartDate AND @EndDate
                );

                DECLARE @Alquileres DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(filtered.amount), 0)
                    FROM (
                        SELECT 
                            h.amount,
                            ROW_NUMBER() OVER (PARTITION BY h.rental_id ORDER BY h.start_date DESC) as rn
                        FROM rental_amount_history h
                        WHERE 
                            h.start_date <= @EndDate
                            AND (h.end_date IS NULL OR h.end_date >= @StartDate)
                    ) filtered
                    WHERE filtered.rn = 1
                );

                DECLARE @Intereses DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(amount), 0) 
                    FROM account_movements 
                    WHERE movement_type = 'DEBITO' 
                      AND concept LIKE 'Interés por mora%'
                      AND movement_date BETWEEN @StartDate AND @EndDate
                );

                DECLARE @SaldoHistorico DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(
                        CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END
                    ), 0)
                    FROM account_movements
                    WHERE movement_date < @StartDate
                );

                DECLARE @DeudaPeriodo DECIMAL(18, 2) = @SaldoHistorico + @Alquileres;


                
                DECLARE @BalanceGlobal DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(
                        CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END
                    ), 0)
                    FROM account_movements
                );

                
                DECLARE @EspaciosOcupados INT = (
                    SELECT ISNULL(SUM(occupied_spaces), 0)
                    FROM rentals
                    WHERE active = 1
                );

                SELECT 
                    @Pagado AS TotalPagado,
                    @Alquileres AS TotalAlquileres,
                    @Intereses AS TotalIntereses,
                    @DeudaPeriodo AS DeudaTotalDelMes,
                    @BalanceGlobal AS BalanceGlobalActual,
                    @EspaciosOcupados AS TotalEspaciosOcupados;
            ";

            SqlParameter[] parameters = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate },
                new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = endDate }
            ];

            DataTable table = await accessDB.GetTableAsync("MonthlyStats", query, parameters);

            if (table.Rows.Count > 0)
            {
                DataRow row = table.Rows[0];
                return new MonthlyStatisticsDTO
                {
                    Year = year,
                    Month = month,
                    TotalPagado = Convert.ToDecimal(row["TotalPagado"]),
                    TotalAlquileres = Convert.ToDecimal(row["TotalAlquileres"]),
                    TotalIntereses = Convert.ToDecimal(row["TotalIntereses"]),
                    DeudaTotalDelMes = Convert.ToDecimal(row["DeudaTotalDelMes"]),
                    BalanceGlobalActual = Convert.ToDecimal(row["BalanceGlobalActual"]),
                    TotalEspaciosOcupados = Convert.ToInt32(row["TotalEspaciosOcupados"])
                };
            }

            return new MonthlyStatisticsDTO { Year = year, Month = month };
        }
    }
}
