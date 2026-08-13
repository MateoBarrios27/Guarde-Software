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
            DateTime nextMonth = startDate.AddMonths(1);
            string monthYear = $"{month:D2}/{year}"; // Formato "MM/yyyy"

            SqlParameter[] parameters = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate },
                new SqlParameter("@NextMonth", SqlDbType.DateTime) { Value = nextMonth },
                new SqlParameter("@MonthYear", SqlDbType.VarChar, 7) { Value = monthYear },
                new SqlParameter("@Month", SqlDbType.Int) { Value = month },
                new SqlParameter("@Year", SqlDbType.Int) { Value = year }
            ];

            string queryMain = @"
                -- 1. INGRESOS REALES (Suma de la tabla de pagos físicos del mes)
                DECLARE @Pagado DECIMAL(18, 2) = (
                    SELECT ISNULL(SUM(amount), 0)
                    FROM payments
                    WHERE payment_date >= @StartDate AND payment_date < @NextMonth
                );

                -- 2. OBTENEMOS EL RESTO DE DATOS DESDE LA SÁBANA DE SALDOS
                DECLARE @AdvancePayments DECIMAL(18, 2);
                DECLARE @DeudaPeriodo DECIMAL(18, 2);
                DECLARE @BalanceGlobal DECIMAL(18, 2);
                DECLARE @Intereses DECIMAL(18, 2);
                DECLARE @ObligacionPeriodo DECIMAL(18, 2);
                DECLARE @AplicadoPeriodo DECIMAL(18, 2);
                DECLARE @PendientePeriodo DECIMAL(18, 2);

                SELECT 
                    @AdvancePayments = ISNULL(SUM(cmb.advanced_payment), 0),
                    @DeudaPeriodo = ISNULL(SUM(cmb.previous_balance), 0),
                    @BalanceGlobal = ISNULL(SUM(cmb.balance), 0),
                    @Intereses = ISNULL(SUM(cmb.interests), 0),
                    @ObligacionPeriodo = ISNULL(SUM(cmb.monthly_debits), 0),
                    @AplicadoPeriodo = ISNULL(SUM(cmb.paid + cmb.advanced_payment), 0),
                    @PendientePeriodo = ISNULL(SUM(
                        CASE
                            WHEN cmb.balance > (cmb.paid + cmb.advanced_payment)
                            THEN cmb.balance - (cmb.paid + cmb.advanced_payment)
                            ELSE 0
                        END
                    ), 0)
                FROM client_month_balances cmb
                INNER JOIN rentals r ON cmb.rental_id = r.rental_id
                WHERE cmb.month_year = @MonthYear
                    AND r.start_date < @NextMonth
                    AND (r.end_date IS NULL OR r.end_date >= @StartDate);

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
                            h.start_date < @NextMonth
                            AND r.start_date < @NextMonth
                            AND (r.end_date IS NULL OR r.end_date >= @StartDate)
                            AND (h.end_date IS NULL OR h.end_date >= @StartDate)
                    ) filtered
                    WHERE filtered.rn = 1
                );

                DECLARE @EspaciosContratadosPeriodo INT = (
                    SELECT ISNULL(SUM(r.occupied_spaces), 0)
                    FROM rentals r
                    WHERE r.start_date < @NextMonth
                        AND (r.end_date IS NULL OR r.end_date >= @StartDate)
                );

                -- 4. ESPACIOS OCUPADOS
                -- RESULTADO FINAL DE ESTADÍSTICAS DEL MES
                SELECT 
                    @Pagado AS TotalPagado,
                    @Alquileres AS TotalAlquileres,
                    @Intereses AS TotalIntereses,
                    @DeudaPeriodo AS DeudaTotalDelMes,
                    @BalanceGlobal AS BalanceGlobalActual,
                    @AdvancePayments AS TotalAdvancePayments,
                    @ObligacionPeriodo AS TotalObligacionDelPeriodo,
                    @AplicadoPeriodo AS TotalAplicadoAlPeriodo,
                    @PendientePeriodo AS TotalPendienteDelPeriodo,
                    @EspaciosContratadosPeriodo AS EspaciosContratadosPeriodo;
            ";

            MonthlyStatisticsDTO resultDto = new() 
            { 
                Year = year, 
                Month = month,
                WarehouseRevenues = [] 
            };

            DataTable tableStats = await accessDB.GetTableAsync("MonthlyStats", queryMain, parameters);
            var (ivaFacturaA, ivaFacturaB) = await GetIvaStatisticsAsync(month, year);
            int espaciosContratadosPeriodo = 0;

            if (tableStats.Rows.Count > 0)
            {
                DataRow row = tableStats.Rows[0];
                resultDto.TotalPagado = row["TotalPagado"] != DBNull.Value ? Convert.ToDecimal(row["TotalPagado"]) : 0;
                resultDto.TotalAlquileres = row["TotalAlquileres"] != DBNull.Value ? Convert.ToDecimal(row["TotalAlquileres"]) : 0;
                resultDto.TotalIntereses = row["TotalIntereses"] != DBNull.Value ? Convert.ToDecimal(row["TotalIntereses"]) : 0;
                resultDto.DeudaTotalDelMes = row["DeudaTotalDelMes"] != DBNull.Value ? Convert.ToDecimal(row["DeudaTotalDelMes"]) : 0;
                resultDto.BalanceGlobalActual = row["BalanceGlobalActual"] != DBNull.Value ? Convert.ToDecimal(row["BalanceGlobalActual"]) : 0;
                resultDto.TotalAdvancePayments = row["TotalAdvancePayments"] != DBNull.Value ? Convert.ToDecimal(row["TotalAdvancePayments"]) : 0;
                resultDto.TotalCubiertoAntesDelPeriodo = resultDto.TotalAdvancePayments;
                resultDto.TotalObligacionDelPeriodo = row["TotalObligacionDelPeriodo"] != DBNull.Value ? Convert.ToDecimal(row["TotalObligacionDelPeriodo"]) : 0;
                resultDto.TotalAplicadoAlPeriodo = row["TotalAplicadoAlPeriodo"] != DBNull.Value ? Convert.ToDecimal(row["TotalAplicadoAlPeriodo"]) : 0;
                resultDto.TotalPendienteDelPeriodo = row["TotalPendienteDelPeriodo"] != DBNull.Value ? Convert.ToDecimal(row["TotalPendienteDelPeriodo"]) : 0;
                espaciosContratadosPeriodo = row["EspaciosContratadosPeriodo"] != DBNull.Value ? Convert.ToInt32(row["EspaciosContratadosPeriodo"]) : 0;
                resultDto.TotalIvaFacturaA = ivaFacturaA;
                resultDto.TotalIvaFacturaB = ivaFacturaB;
            }

            string queryWarehouses = @"
                WITH AssignmentRows AS (
                    SELECT l.rental_id, l.warehouse_id, l.locker_id
                    FROM lockers l
                    INNER JOIN rentals r ON r.rental_id = l.rental_id
                    WHERE l.active = 1
                        AND ISNULL(l.is_free_space, 0) = 0
                        AND l.rental_id IS NOT NULL

                    UNION ALL

                    SELECT rl.rental_id, l.warehouse_id, l.locker_id
                    FROM rental_lockers rl
                    INNER JOIN lockers l ON l.locker_id = rl.locker_id AND l.active = 1
                    INNER JOIN rentals r ON r.rental_id = rl.rental_id
                    WHERE ISNULL(l.is_free_space, 0) = 1
                ),
                RentalWarehouseUnits AS (
                    SELECT rental_id, warehouse_id, COUNT(*) AS Units
                    FROM AssignmentRows
                    GROUP BY rental_id, warehouse_id
                ),
                RentalUnitTotals AS (
                    SELECT rental_id, SUM(Units) AS TotalUnits
                    FROM RentalWarehouseUnits
                    GROUP BY rental_id
                ),
                RevenueByWarehouse AS (
                    SELECT
                        rwu.warehouse_id,
                        SUM(am.amount * CAST(rwu.Units AS DECIMAL(18, 4)) / NULLIF(rut.TotalUnits, 0)) AS Revenue
                    FROM account_movements am
                    INNER JOIN RentalWarehouseUnits rwu ON rwu.rental_id = am.rental_id
                    INNER JOIN RentalUnitTotals rut ON rut.rental_id = am.rental_id
                    WHERE am.movement_type = 'CREDITO'
                        AND am.movement_date >= @StartDate
                        AND am.movement_date < @NextMonth
                    GROUP BY rwu.warehouse_id
                ),
                UnassignedRevenue AS (
                    SELECT ISNULL(SUM(am.amount), 0) AS Revenue
                    FROM account_movements am
                    WHERE am.movement_type = 'CREDITO'
                        AND am.movement_date >= @StartDate
                        AND am.movement_date < @NextMonth
                        AND NOT EXISTS (
                            SELECT 1
                            FROM RentalUnitTotals rut
                            WHERE rut.rental_id = am.rental_id
                        )
                ),
                AssignedLockers AS (
                    SELECT l.locker_id
                    FROM lockers l
                    INNER JOIN rentals r ON r.rental_id = l.rental_id AND r.active = 1
                    WHERE l.active = 1
                        AND ISNULL(l.is_free_space, 0) = 0
                        AND l.rental_id IS NOT NULL

                    UNION

                    SELECT l.locker_id
                    FROM rental_lockers rl
                    INNER JOIN lockers l ON l.locker_id = rl.locker_id AND l.active = 1
                    INNER JOIN rentals r ON r.rental_id = rl.rental_id AND r.active = 1
                    WHERE ISNULL(l.is_free_space, 0) = 1
                ),
                OccupancyByWarehouse AS (
                    SELECT
                        w.warehouse_id,
                        w.name,
                        w.address,
                        COUNT(l.locker_id) AS TotalSpaces,
                        SUM(CASE
                            WHEN l.locker_id IS NULL THEN 0
                            WHEN UPPER(ISNULL(l.status, '')) = 'OCUPADO' THEN 1
                            WHEN assigned.locker_id IS NOT NULL THEN 1
                            ELSE 0
                        END) AS OccupiedSpaces
                    FROM warehouses w
                    LEFT JOIN lockers l
                        ON l.warehouse_id = w.warehouse_id
                        AND l.active = 1
                        AND UPPER(ISNULL(l.status, '')) <> 'ELIMINADO'
                    LEFT JOIN AssignedLockers assigned ON assigned.locker_id = l.locker_id
                    WHERE ISNULL(w.active, 1) = 1
                    GROUP BY w.warehouse_id, w.name, w.address
                )
                SELECT
                    o.name AS WarehouseName,
                    o.address AS WarehouseAddress,
                    ISNULL(r.Revenue, 0) AS Revenue,
                    o.TotalSpaces,
                    o.OccupiedSpaces
                FROM OccupancyByWarehouse o
                LEFT JOIN RevenueByWarehouse r ON r.warehouse_id = o.warehouse_id

                UNION ALL

                SELECT
                    'Sin depósito asignado' AS WarehouseName,
                    'Cobros de alquileres sin espacios vinculados' AS WarehouseAddress,
                    ur.Revenue,
                    0 AS TotalSpaces,
                    0 AS OccupiedSpaces
                FROM UnassignedRevenue ur
                WHERE ur.Revenue <> 0

                ORDER BY WarehouseName;
            ";
            
            SqlParameter[] parametersWh = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate },
                new SqlParameter("@NextMonth", SqlDbType.DateTime) { Value = nextMonth }
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
                        Revenue = row["Revenue"] != DBNull.Value ? Convert.ToDecimal(row["Revenue"]) : 0,
                        TotalSpaces = row["TotalSpaces"] != DBNull.Value ? Convert.ToInt32(row["TotalSpaces"]) : 0,
                        OccupiedSpaces = row["OccupiedSpaces"] != DBNull.Value ? Convert.ToInt32(row["OccupiedSpaces"]) : 0
                    });
                }
            }

            foreach (WarehouseRevenueDto warehouse in resultDto.WarehouseRevenues)
            {
                warehouse.OccupancyPercentage = warehouse.TotalSpaces > 0
                    ? Math.Round((decimal)warehouse.OccupiedSpaces / warehouse.TotalSpaces * 100m, 1)
                    : 0m;
            }

            resultDto.TotalEspacios = resultDto.WarehouseRevenues.Sum(w => w.TotalSpaces);
            resultDto.TotalEspaciosOcupados = resultDto.WarehouseRevenues.Sum(w => w.OccupiedSpaces);
            resultDto.PorcentajeOcupacion = resultDto.TotalEspacios > 0
                ? Math.Round((decimal)resultDto.TotalEspaciosOcupados / resultDto.TotalEspacios * 100m, 1)
                : 0m;
            resultDto.AbonoPromedioPorEspacio = espaciosContratadosPeriodo > 0
                ? Math.Round(resultDto.TotalAlquileres / espaciosContratadosPeriodo, 2)
                : 0m;

            string queryPaymentAllocation = @"
                WITH DebitLedger AS (
                    SELECT
                        am.rental_id,
                        am.movement_id,
                        DATEFROMPARTS(YEAR(am.movement_date), MONTH(am.movement_date), 1) AS TargetMonth,
                        am.amount,
                        SUM(am.amount) OVER (
                            PARTITION BY am.rental_id
                            ORDER BY am.movement_date, am.movement_id
                            ROWS UNBOUNDED PRECEDING
                        ) AS DebitEnd
                    FROM account_movements am
                    WHERE am.movement_type = 'DEBITO' AND am.amount > 0
                ),
                DebitRanges AS (
                    SELECT *, DebitEnd - amount AS DebitStart
                    FROM DebitLedger
                ),
                CreditSequenced AS (
                    SELECT
                        am.rental_id,
                        am.movement_id,
                        am.movement_date AS CreditDate,
                        am.payment_id,
                        am.amount,
                        ROW_NUMBER() OVER (
                            PARTITION BY am.payment_id
                            ORDER BY am.movement_id
                        ) AS PaymentSequence
                    FROM account_movements am
                    WHERE am.movement_type = 'CREDITO' AND am.amount > 0
                ),
                CreditLedger AS (
                    SELECT
                        cs.*,
                        SUM(cs.amount) OVER (
                            PARTITION BY cs.rental_id
                            ORDER BY cs.CreditDate, cs.movement_id
                            ROWS UNBOUNDED PRECEDING
                        ) AS CreditEnd
                    FROM CreditSequenced cs
                ),
                CreditRanges AS (
                    SELECT *, CreditEnd - amount AS CreditStart
                    FROM CreditLedger
                ),
                Allocations AS (
                    SELECT
                        d.TargetMonth,
                        p.payment_date AS PaymentDate,
                        CASE
                            WHEN bounds.OverlapEnd > bounds.OverlapStart
                            THEN bounds.OverlapEnd - bounds.OverlapStart
                            ELSE 0
                        END AS AppliedAmount
                    FROM DebitRanges d
                    INNER JOIN CreditRanges c
                        ON c.rental_id = d.rental_id
                        AND c.payment_id IS NOT NULL
                        AND c.PaymentSequence = 1
                        AND d.DebitEnd > c.CreditStart
                        AND c.CreditEnd > d.DebitStart
                    INNER JOIN payments p ON p.payment_id = c.payment_id
                    CROSS APPLY (
                        VALUES (
                            CASE WHEN d.DebitEnd < c.CreditEnd THEN d.DebitEnd ELSE c.CreditEnd END,
                            CASE WHEN d.DebitStart > c.CreditStart THEN d.DebitStart ELSE c.CreditStart END
                        )
                    ) bounds(OverlapEnd, OverlapStart)
                )
                SELECT
                    ISNULL(SUM(CASE
                        WHEN PaymentDate >= @StartDate
                            AND PaymentDate < @NextMonth
                            AND TargetMonth >= @NextMonth
                        THEN AppliedAmount ELSE 0
                    END), 0) AS CollectedForFutureMonths
                FROM Allocations;
            ";

            SqlParameter[] allocationParameters = [
                new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = startDate },
                new SqlParameter("@NextMonth", SqlDbType.DateTime) { Value = nextMonth }
            ];

            DataTable allocationTable = await accessDB.GetTableAsync(
                "PaymentAllocationStats",
                queryPaymentAllocation,
                allocationParameters
            );

            if (allocationTable.Rows.Count > 0)
            {
                DataRow allocation = allocationTable.Rows[0];
                resultDto.TotalCobradoParaMesesFuturos = allocation["CollectedForFutureMonths"] != DBNull.Value
                    ? Convert.ToDecimal(allocation["CollectedForFutureMonths"])
                    : 0m;
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
                            WHEN ISNULL(r.months_unpaid, 0) >= 1 THEN 'Moroso'
                            WHEN (
                                CASE 
                                    WHEN step1.LastBalanceDate IS NULL OR step1.LastBalanceDate < CAST(DATEADD(hour, -3, GETUTCDATE()) AS DATE)
                                    THEN DATEFROMPARTS(YEAR(DATEADD(hour, -3, GETUTCDATE())), MONTH(DATEADD(hour, -3, GETUTCDATE())), 10)
                                    ELSE step1.LastBalanceDate
                                END
                            ) > EOMONTH(DATEADD(hour, -3, GETUTCDATE())) THEN 'PagaronElMes'
                            ELSE 'NoPagaronElMes'
                        END AS Status
                    FROM clients c
                    OUTER APPLY (
                        SELECT TOP 1 *
                        FROM rentals r_sub
                        WHERE r_sub.client_id = c.client_id 
                          AND (r_sub.active = 1 OR c.active = 0)
                        ORDER BY r_sub.active DESC, r_sub.start_date DESC, r_sub.rental_id DESC
                    ) r
                    OUTER APPLY (
                        SELECT TOP 1
                            LastBalanceDate = TRY_CONVERT(date, CONCAT('01/', cmb.month_year), 103)
                        FROM client_month_balances cmb
                        WHERE cmb.rental_id = r.rental_id
                        ORDER BY cmb.id DESC
                    ) step1
                )
                SELECT 
                    SUM(CASE WHEN Status <> 'Baja' THEN 1 ELSE 0 END) as Total,
                    
                    SUM(CASE WHEN Status = 'PagaronElMes' THEN 1 ELSE 0 END) as AlDia,
                    SUM(CASE WHEN Status = 'Moroso' THEN 1 ELSE 0 END) as Morosos,
                    SUM(CASE WHEN Status = 'NoPagaronElMes' THEN 1 ELSE 0 END) as Pendientes,
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
