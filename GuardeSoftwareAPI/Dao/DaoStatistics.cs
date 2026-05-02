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

            SqlParameter[] parameters = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate },
                new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = endDate }
            ];

            string queryMain = @"
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

                DECLARE @Intereses DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(amount), 0) 
                    FROM account_movements 
                    WHERE movement_type = 'DEBITO' 
                        AND concept LIKE 'Interés por mora%'
                        AND movement_date BETWEEN @StartDate AND @EndDate
                );

                DECLARE @SaldoHistorico DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(
                        CASE WHEN movement_type = 'DEBITO' THEN -amount ELSE amount END
                    ), 0)
                    FROM account_movements
                    WHERE movement_date < @StartDate
                );

                DECLARE @DeudaPeriodo DECIMAL(18, 2) = @SaldoHistorico;

                DECLARE @BalanceGlobal DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(
                        CASE WHEN movement_type = 'DEBITO' THEN -amount ELSE amount END
                    ), 0)
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE r.active = 1
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
                resultDto.DeudaTotalDelMes = await GetPreviousPeriodDebtAsync(month, year);
                resultDto.BalanceGlobalActual = row["BalanceGlobalActual"] != DBNull.Value ? Convert.ToDecimal(row["BalanceGlobalActual"]) : 0;
                resultDto.TotalEspaciosOcupados = row["TotalEspaciosOcupados"] != DBNull.Value ? Convert.ToInt32(row["TotalEspaciosOcupados"]) : 0;
                resultDto.TotalAdvancePayments = await GetTotalAdvancePaymentsAsync(month, year);
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

        public async Task<decimal> GetTotalAdvancePaymentsAsync(int month, int year)
        {
            string query = @"
                -- 1. BALANCE GLOBAL POSITIVO (Filtro de seguridad)
                -- Solo consideramos clientes que 'hoy' tienen plata a favor.
                WITH PositiveBalances AS (
                    SELECT
                        r.client_id,
                        SUM(
                            CASE 
                                WHEN am.movement_type = 'DEBITO' THEN -am.amount 
                                ELSE am.amount 
                            END
                        ) AS CurrentGlobalBalance
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE r.active = 1
                    GROUP BY r.client_id
                    HAVING SUM(CASE WHEN am.movement_type = 'DEBITO' THEN -am.amount ELSE am.amount END) > 0
                ),

                -- 2. CRÉDITOS REALES DEL MES (Dinero a favor generado este mes)
                -- Aquí sumamos Pagos, Notas de Crédito, Ajustes, etc.
                -- Todo lo que NO sea 'DEBITO'.
                MonthlyCredits AS (
                    SELECT 
                        r.client_id,
                        ISNULL(SUM(am.amount), 0) as TotalCreditedInMonth
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE am.movement_type <> 'DEBITO' -- <--- EL CAMBIO CLAVE
                    AND MONTH(am.movement_date) = @Month
                    AND YEAR(am.movement_date) = @Year
                    GROUP BY r.client_id
                ),

                -- 3. DÉBITOS REALES DEL MES (Lo que se cobró: Alquiler, Proporcionales, etc)
                MonthlyDebits AS (
                    SELECT 
                        r.client_id,
                        ISNULL(SUM(am.amount), 0) as TotalDebitedInMonth
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE am.movement_type = 'DEBITO' 
                    AND MONTH(am.movement_date) = @Month
                    AND YEAR(am.movement_date) = @Year
                    GROUP BY r.client_id
                )

                -- CÁLCULO FINAL:
                -- (Créditos del Mes - Débitos del Mes) = Excedente generado este mes.
                -- Ajustado por el tope de su saldo global real actual.
                SELECT ISNULL(SUM(
                    CASE 
                        -- Si el excedente del mes es mayor que su saldo global actual, 
                        -- significa que parte de ese crédito se usó para cubrir deuda vieja que ya no existe,
                        -- o que el saldo se consumió después. Solo tomamos el 'saldo vivo'.
                        WHEN (mc.TotalCreditedInMonth - ISNULL(md.TotalDebitedInMonth, 0)) > pb.CurrentGlobalBalance 
                        THEN pb.CurrentGlobalBalance
                        
                        -- Caso normal: El adelanto es la diferencia pura generada en este mes
                        ELSE (mc.TotalCreditedInMonth - ISNULL(md.TotalDebitedInMonth, 0))
                    END
                ), 0)
                FROM MonthlyCredits mc
                INNER JOIN PositiveBalances pb ON mc.client_id = pb.client_id
                LEFT JOIN MonthlyDebits md ON mc.client_id = md.client_id
                WHERE 
                    -- Solo sumamos si los Créditos superaron a los Débitos este mes
                    mc.TotalCreditedInMonth > ISNULL(md.TotalDebitedInMonth, 0);
            ";

            var parameters = new[] {
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            };

            var result = await accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToDecimal(result);
        }   

        public async Task<decimal> GetPreviousPeriodDebtAsync(int month, int year)
        {
            // Lógic:
            // 1. We calculate the global balance for each client up to the end of the previous month (month-1, year).
            // 2. We filter only those clients whose balance was negative (debt) at that point in time, ignoring those with zero or positive balance (credit).
            // 3. We sum the absolute value of those negative balances to get the total debt from previous periods that was still pending at the start of the given month/year.

            string query = @"
                DECLARE @StartOfMonth DATE = DATEFROMPARTS(@Year, @Month, 1);

                WITH HistoricalBalances AS (
                    SELECT
                        r.client_id,
                        SUM(
                            CASE 
                                WHEN am.movement_type = 'DEBITO' THEN -am.amount 
                                ELSE am.amount 
                            END
                        ) AS BalanceAlCierreAnterior
                    FROM account_movements am
                    INNER JOIN rentals r ON am.rental_id = r.rental_id
                    WHERE r.active = 1
                    AND am.movement_date < @StartOfMonth 
                    GROUP BY r.client_id
                )
                
                SELECT ISNULL(ABS(SUM(BalanceAlCierreAnterior)), 0)
                FROM HistoricalBalances
                WHERE BalanceAlCierreAnterior < 0;
            ";

            var parameters = new[] {
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year)
            };

            var result = await accessDB.ExecuteScalarAsync(query, parameters);
            return Convert.ToDecimal(result);
        }
    }
}
