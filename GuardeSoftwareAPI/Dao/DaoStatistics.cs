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

        public async Task<ClientStatisticsDto> GetClientStatisticsAsync()
        {
            string query = @"
                WITH ClientStatus AS (
                    SELECT 
                        c.client_id,
                        CASE 
                            WHEN c.active = 0 THEN 'Baja'
                            WHEN ISNULL(r_stats.total_months_unpaid, 0) >= 1 THEN 'Moroso'
                            WHEN ISNULL(acc_stats.balance, 0) <= 0 THEN 'AlDia'
                            ELSE 'Pendiente'
                        END AS Status
                    FROM clients c
                    LEFT JOIN (
                        SELECT client_id, SUM(months_unpaid) as total_months_unpaid 
                        FROM rentals WHERE active = 1 GROUP BY client_id
                    ) r_stats ON c.client_id = r_stats.client_id
                    LEFT JOIN (
                        SELECT r.client_id, 
                               SUM(am.amount * CASE WHEN am.movement_type = 'DEBITO' THEN 1 ELSE -1 END) as balance 
                        FROM rentals r 
                        JOIN account_movements am ON r.rental_id = am.rental_id 
                        GROUP BY r.client_id
                    ) acc_stats ON c.client_id = acc_stats.client_id
                )
                SELECT 
                    -- CAMBIO: Total ahora solo cuenta los que NO son 'Baja'
                    SUM(CASE WHEN Status <> 'Baja' THEN 1 ELSE 0 END) as Total,
                    
                    SUM(CASE WHEN Status = 'AlDia' THEN 1 ELSE 0 END) as AlDia,
                    SUM(CASE WHEN Status = 'Moroso' THEN 1 ELSE 0 END) as Morosos,
                    SUM(CASE WHEN Status = 'Pendiente' THEN 1 ELSE 0 END) as Pendientes,
                    SUM(CASE WHEN Status = 'Baja' THEN 1 ELSE 0 END) as DadosBaja
                FROM ClientStatus;
            ";

            using (var result = await accessDB.GetTableAsync("Statistics", query))
            {
                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    // Agregamos chequeos de DBNull por seguridad (SUM devuelve NULL si no hay filas)
                    return new ClientStatisticsDto
                    {
                        Total = row["Total"] != DBNull.Value ? Convert.ToInt32(row["Total"]) : 0,
                        AlDia = row["AlDia"] != DBNull.Value ? Convert.ToInt32(row["AlDia"]) : 0,
                        Morosos = row["Morosos"] != DBNull.Value ? Convert.ToInt32(row["Morosos"]) : 0,
                        Pendientes = row["Pendientes"] != DBNull.Value ? Convert.ToInt32(row["Pendientes"]) : 0,
                        DadosBaja = row["DadosBaja"] != DBNull.Value ? Convert.ToInt32(row["DadosBaja"]) : 0
                    };
                }
            }
            return new ClientStatisticsDto();
        }
    }
}
