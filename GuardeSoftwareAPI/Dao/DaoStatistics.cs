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
            string monthYear = $"{month:D2}/{year}"; // Formato "MM/yyyy"

            SqlParameter[] parameters = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate },
                new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = endDate },
                new SqlParameter("@MonthYear", SqlDbType.VarChar, 7) { Value = monthYear },
                new SqlParameter("@Month", SqlDbType.Int) { Value = month },
                new SqlParameter("@Year", SqlDbType.Int) { Value = year }
            ];

            string queryMain = @"
                -- 1. INGRESOS REALES (Suma de la tabla de pagos físicos del mes)
                DECLARE @Pagado DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(amount), 0)
                    FROM payments
                    WHERE MONTH(payment_date) = @Month AND YEAR(payment_date) = @Year
                );

                -- 2. OBTENEMOS EL RESTO DE DATOS DESDE LA SÁBANA DE SALDOS
                DECLARE @AdvancePayments DECIMAL(18, 2);
                DECLARE @DeudaPeriodo DECIMAL(18, 2);
                DECLARE @BalanceGlobal DECIMAL(18, 2);
                DECLARE @Intereses DECIMAL(18, 2);

                SELECT 
                    @AdvancePayments = ISNULL(SUM(cmb.advanced_payment), 0),
                    @DeudaPeriodo = ISNULL(SUM(cmb.previous_balance), 0),
                    @BalanceGlobal = ISNULL(SUM(cmb.balance), 0),
                    @Intereses = ISNULL(SUM(cmb.interests), 0)
                FROM client_month_balances cmb
                INNER JOIN rentals r ON cmb.rental_id = r.rental_id
                WHERE r.active = 1 AND cmb.month_year = @MonthYear;

                -- 3. ALQUILERES HISTÓRICOS (Se mantiene igual porque depende del historial de montos)
                DECLARE @Alquileres DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(filtered.amount), 0)
                    FROM (
                        SELECT 
                            h.amount,
                            ROW_NUMBER() OVER (
                                PARTITION BY h.rental_id 
                                ORDER BY 
                                    h.start_date DESC, 
                                    CASE WHEN h.end_date IS NULL THEN 1 ELSE 0 END DESC, 
                                    h.rental_amount_history_id DESC
                            ) as rn
                        FROM rental_amount_history h
                        INNER JOIN rentals r ON h.rental_id = r.rental_id
                        WHERE 
                            r.active = 1
                            AND h.start_date <= @EndDate
                            AND (h.end_date IS NULL OR h.end_date >= @StartDate)
                    ) filtered
                    WHERE filtered.rn = 1
                );

                -- 4. ESPACIOS OCUPADOS
                DECLARE @EspaciosOcupados INT = (
                    SELECT ISNULL(SUM(occupied_spaces), 0)
                    FROM rentals
                    WHERE active = 1
                );

                -- RESULTADO FINAL DE ESTADÍSTICAS DEL MES
                SELECT 
                    @Pagado AS TotalPagado,
                    @Alquileres AS TotalAlquileres,
                    @Intereses AS TotalIntereses,
                    @DeudaPeriodo AS DeudaTotalDelMes,
                    @BalanceGlobal AS BalanceGlobalActual,
                    @EspaciosOcupados AS TotalEspaciosOcupados,
                    @AdvancePayments AS TotalAdvancePayments;
            ";

            MonthlyStatisticsDTO resultDto = new() 
            { 
                Year = year, 
                Month = month,
                WarehouseRevenues = [] 
            };

            DataTable tableStats = await accessDB.GetTableAsync("MonthlyStats", queryMain, parameters);
            var (ivaFacturaA, ivaFacturaB) = await GetIvaStatisticsAsync(month, year);

            if (tableStats.Rows.Count > 0)
            {
                DataRow row = tableStats.Rows[0];
                resultDto.TotalPagado = row["TotalPagado"] != DBNull.Value ? Convert.ToDecimal(row["TotalPagado"]) : 0;
                resultDto.TotalAlquileres = row["TotalAlquileres"] != DBNull.Value ? Convert.ToDecimal(row["TotalAlquileres"]) : 0;
                resultDto.TotalIntereses = row["TotalIntereses"] != DBNull.Value ? Convert.ToDecimal(row["TotalIntereses"]) : 0;
                resultDto.DeudaTotalDelMes = row["DeudaTotalDelMes"] != DBNull.Value ? Convert.ToDecimal(row["DeudaTotalDelMes"]) : 0;
                resultDto.BalanceGlobalActual = row["BalanceGlobalActual"] != DBNull.Value ? Convert.ToDecimal(row["BalanceGlobalActual"]) : 0;
                resultDto.TotalEspaciosOcupados = row["TotalEspaciosOcupados"] != DBNull.Value ? Convert.ToInt32(row["TotalEspaciosOcupados"]) : 0;
                resultDto.TotalAdvancePayments = row["TotalAdvancePayments"] != DBNull.Value ? Convert.ToDecimal(row["TotalAdvancePayments"]) : 0;
                resultDto.TotalIvaFacturaA = ivaFacturaA;
                resultDto.TotalIvaFacturaB = ivaFacturaB;
            }

            string queryWarehouses = @"
                SELECT 
                    w.name AS WarehouseName,
                    w.address AS WarehouseAddress,
                    ISNULL(SUM(am.amount), 0) AS Revenue
                FROM account_movements am
                INNER JOIN rentals r ON am.rental_id = r.rental_id
                INNER JOIN (
                    SELECT rental_id, MIN(warehouse_id) as warehouse_id
                    FROM lockers
                    WHERE rental_id IS NOT NULL
                    GROUP BY rental_id
                ) l_uniq ON r.rental_id = l_uniq.rental_id
                INNER JOIN warehouses w ON l_uniq.warehouse_id = w.warehouse_id
                WHERE am.movement_type = 'CREDITO'
                    AND am.movement_date BETWEEN @StartDate AND @EndDate
                GROUP BY w.name, w.address
                ORDER BY Revenue DESC
            ";
            
            SqlParameter[] parametersWh = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate },
                new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = endDate }
            ];

            DataTable tableWarehouses = await accessDB.GetTableAsync("WarehouseStats", queryWarehouses, parametersWh);

            if (tableWarehouses.Rows.Count > 0)
            {
                foreach (DataRow row in tableWarehouses.Rows)
                {
                    resultDto.WarehouseRevenues.Add(new WarehouseRevenueDto
                    {
                        Name = row["WarehouseName"].ToString() ?? string.Empty,
                        Address = row["WarehouseAddress"].ToString() ?? string.Empty,
                        Revenue = row["Revenue"] != DBNull.Value ? Convert.ToDecimal(row["Revenue"]) : 0
                    });
                }
            }

            return resultDto;
        }

        public async Task<(decimal IvaFacturaA, decimal IvaFacturaB)> GetIvaStatisticsAsync(int month, int year)
        {
            string query = @"
                SELECT 
                    ISNULL(SUM(CASE 
                        WHEN bt.name LIKE 'Factura A%' 
                        THEN am.amount * 0.21 
                        ELSE 0 
                    END), 0) AS IvaFacturaA,
                    
                    ISNULL(SUM(CASE 
                        WHEN bt.name LIKE 'Factura B%' 
                        THEN am.amount * 0.21 
                        ELSE 0 
                    END), 0) AS IvaFacturaB

                FROM account_movements am
                LEFT JOIN payments p ON am.payment_id = p.payment_id
                LEFT JOIN payment_methods pm ON p.payment_method_id = pm.payment_method_id
                LEFT JOIN clients c_pay ON p.client_id = c_pay.client_id
                LEFT JOIN rentals r ON am.rental_id = r.rental_id
                LEFT JOIN clients c_rent ON r.client_id = c_rent.client_id
                
                LEFT JOIN billing_types bt ON bt.billing_type_id = ISNULL(c_pay.billing_type_id, c_rent.billing_type_id)
                
                WHERE 
                    am.movement_type = 'CREDITO' 
                    AND MONTH(am.movement_date) = @Month 
                    AND YEAR(am.movement_date) = @Year
                    AND ISNULL(pm.name, '') <> 'Efectivo'";

            using SqlConnection connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add(new SqlParameter("@Month", month));
            cmd.Parameters.Add(new SqlParameter("@Year", year));

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (
                    Convert.ToDecimal(reader["IvaFacturaA"]),
                    Convert.ToDecimal(reader["IvaFacturaB"])
                );
            }

            return (0m, 0m);
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

        public async Task<decimal> GetTotalAdvancePaymentsAsync(int month, int year)
        {
            string monthYear = $"{month:D2}/{year}"; 
            
            string query = @"
                SELECT ISNULL(SUM(cmb.advanced_payment), 0)
                FROM client_month_balances cmb
                INNER JOIN rentals r ON cmb.rental_id = r.rental_id
                WHERE r.active = 1 AND cmb.month_year = @MonthYear;
            ";

            var parameters = new[] {
                new SqlParameter("@MonthYear", SqlDbType.VarChar, 7) { Value = monthYear }
            };

            var result = await accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToDecimal(result);
        }   

        public async Task<decimal> GetPreviousPeriodDebtAsync(int month, int year)
        {
            string monthYear = $"{month:D2}/{year}"; 

            string query = @"
                SELECT ISNULL(SUM(cmb.previous_balance), 0)
                FROM client_month_balances cmb
                INNER JOIN rentals r ON cmb.rental_id = r.rental_id
                WHERE r.active = 1 AND cmb.month_year = @MonthYear;
            ";

            var parameters = new[] {
                new SqlParameter("@MonthYear", SqlDbType.VarChar, 7) { Value = monthYear }
            };

            var result = await accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToDecimal(result);
        }
    }
}