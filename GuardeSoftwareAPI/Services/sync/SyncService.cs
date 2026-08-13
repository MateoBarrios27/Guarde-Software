using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Dtos.Sync;
using GuardeSoftwareAPI.Services.payment;
using System.Data;

namespace GuardeSoftwareAPI.Services.sync
{
    public class SyncService : ISyncService
    {
        private readonly AccessDB _accessDB;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<SyncService> _logger;

        public SyncService(AccessDB accessDB, IPaymentService paymentService, ILogger<SyncService> logger)
        {
            _accessDB = accessDB;
            _paymentService = paymentService;
            _logger = logger;
        }

        public async Task<SyncSnapshotDto> GetSnapshotAsync()
        {
            var snapshot = new SyncSnapshotDto
            {
                GeneratedAt = DateTime.UtcNow
            };

            // Load active clients using the same full calculation logic as GetTableClientsAsync
            var clientsQuery = @"
                WITH CurrentRentalAmount AS (
                    SELECT h.rental_id, h.amount AS CurrentRent
                    FROM (
                        SELECT rental_id, amount,
                               ROW_NUMBER() OVER (PARTITION BY rental_id ORDER BY start_date DESC, CASE WHEN end_date IS NULL THEN 1 ELSE 0 END DESC, rental_amount_history_id DESC) as rn
                        FROM rental_amount_history WHERE start_date <= DATEADD(hour, -3, GETUTCDATE())
                    ) h WHERE h.rn = 1
                )
                SELECT
                    c.client_id                         AS Id,
                    c.full_name                         AS FullName,
                    c.payment_identifier                AS PaymentIdentifier,
                    c.color                             AS Color,
                    c.active                            AS Active,
                    c.preferred_payment_method_id       AS PreferredPaymentMethodId,
                    c.increase_frequency_months         AS IncreaseFrequencyMonths,
                    c.is_six_month_promotion             AS IsSixMonthPromotion,
                    r.rental_id                         AS RentalId,
                    r.increase_anchor_date              AS IncreaseAnchorDate,

                    -- PendingSurcharge from rentals (not on clients table)
                    (
                        SELECT SUM(r2.pending_surcharge)
                        FROM rentals r2
                        WHERE r2.client_id = c.client_id AND r2.active = 1
                    ) AS PendingSurcharge,

                    -- MonthsUnpaid from rentals
                    (
                        SELECT SUM(ISNULL(r2.months_unpaid, 0))
                        FROM rentals r2
                        WHERE r2.client_id = c.client_id AND r2.active = 1
                    ) AS MonthsUnpaid,

                    -- Calculated financial fields (same cascade logic as GetTableClientsAsync)
                    step1.UI_InterestAmount             AS InterestAmount,
                    ISNULL(step1.UI_CurrentRent, ISNULL(cr.CurrentRent, 0)) AS CurrentRent,
                    step1.UI_Balance                    AS Balance,
                    step1.UI_PreviousBalance            AS PreviousBalance,
                    db.MonthYearDB                      AS LastGeneratedMonthYear,

                    -- NextPaymentDay (same logic as GetTableClientsAsync)
                    CASE
                        WHEN c.active = 0 THEN NULL
                        WHEN step1.LastBalanceDate IS NULL OR step1.LastBalanceDate < CAST(DATEADD(hour, -3, GETUTCDATE()) AS DATE)
                        THEN CAST(DATEFROMPARTS(YEAR(DATEADD(hour, -3, GETUTCDATE())), MONTH(DATEADD(hour, -3, GETUTCDATE())), 1) AS VARCHAR(10))
                        ELSE CAST(step1.LastBalanceDate AS VARCHAR(10))
                    END AS NextPaymentDay,

                    ISNULL(plannedPayment.PlannedAmount, 0) AS PlannedPaymentAmount,
                    ISNULL(plannedPayment.HasPlannedPayment, 0) AS HasPlannedPayment,

                    -- Status
                    CASE
                        WHEN c.active = 0 THEN 'Baja'
                        WHEN (SELECT SUM(ISNULL(r2.months_unpaid, 0)) FROM rentals r2 WHERE r2.client_id = c.client_id AND r2.active = 1) >= 1 THEN 'Moroso'
                        WHEN ISNULL(step1.UI_Balance, 0) >= 0 THEN 'Al día'
                        ELSE 'Pendiente'
                    END AS Status

                FROM clients c
                OUTER APPLY (
                    SELECT TOP 1 *
                    FROM rentals r_sub
                    WHERE r_sub.client_id = c.client_id AND r_sub.active = 1
                    ORDER BY r_sub.start_date DESC, r_sub.rental_id DESC
                ) r
                LEFT JOIN CurrentRentalAmount cr ON r.rental_id = cr.rental_id

                OUTER APPLY (
                    SELECT
                        PlannedAmount = SUM(remaining.RentAmount),
                        HasPlannedPayment = CASE
                            WHEN SUM(CASE
                                WHEN monthInfo.MonthStart > DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
                                     AND remaining.RentAmount > 0
                                THEN 1 ELSE 0 END) > 0
                            THEN CAST(1 AS bit) ELSE CAST(0 AS bit)
                        END
                    FROM client_month_balances cmb
                    CROSS APPLY (
                        SELECT MonthStart = DATEFROMPARTS(
                            CONVERT(int, RIGHT(cmb.month_year, 4)),
                            CONVERT(int, LEFT(cmb.month_year, 2)),
                            1)
                    ) monthInfo
                    CROSS APPLY (
                        SELECT RentPaid = CASE
                            WHEN (ISNULL(cmb.paid, 0) + ISNULL(cmb.advanced_payment, 0)
                                  - ISNULL(cmb.previous_balance, 0) - ISNULL(cmb.interests, 0)) > 0
                            THEN (ISNULL(cmb.paid, 0) + ISNULL(cmb.advanced_payment, 0)
                                  - ISNULL(cmb.previous_balance, 0) - ISNULL(cmb.interests, 0))
                            ELSE 0
                        END
                    ) applied
                    CROSS APPLY (
                        SELECT RentAmount = CASE
                            WHEN ISNULL(cmb.monthly_debits, 0) > applied.RentPaid
                            THEN ISNULL(cmb.monthly_debits, 0) - applied.RentPaid
                            ELSE 0
                        END
                    ) remaining
                    WHERE cmb.rental_id = r.rental_id
                      AND monthInfo.MonthStart >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
                      AND EXISTS (
                          SELECT 1
                          FROM account_movements am
                          WHERE am.rental_id = cmb.rental_id
                            AND am.movement_type = 'DEBITO'
                            AND am.payment_id IS NULL
                            AND am.concept LIKE 'Alquiler %'
                            AND DATEFROMPARTS(YEAR(am.movement_date), MONTH(am.movement_date), 1) = monthInfo.MonthStart
                      )
                ) plannedPayment

                -- Last generated month (most recent)
                OUTER APPLY (
                    SELECT TOP 1
                        MonthYearDB = cmb.month_year,
                        NetBalance = cmb.balance - cmb.paid - cmb.advanced_payment
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                    ORDER BY cmb.id DESC
                ) latest_cmb

                -- Active unpaid month
                OUTER APPLY (
                    SELECT TOP 1
                        Id = cmb.id,
                        PrevBalDB = ISNULL(cmb.previous_balance, 0),
                        IntsDB = ISNULL(cmb.interests, 0),
                        RentDB = CASE WHEN ISNULL(cmb.monthly_debits, 0) = 0 THEN ISNULL(cr.CurrentRent, 0) ELSE cmb.monthly_debits END,
                        PaidDB = ISNULL(cmb.paid, 0),
                        AdvPayDB = ISNULL(cmb.advanced_payment, 0),
                        MonthYearDB = cmb.month_year
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                      AND (cmb.balance - cmb.paid - cmb.advanced_payment) > 0
                    ORDER BY cmb.id DESC
                ) db

                -- Raw previous balance and interest
                OUTER APPLY (
                    SELECT
                        Raw_PrevBal = ISNULL((
                            SELECT SUM(
                                CASE
                                    WHEN ISNULL(cmb2.monthly_debits, 0) - ISNULL(cmb2.paid, 0) - ISNULL(cmb2.advanced_payment, 0) > 0
                                    THEN ISNULL(cmb2.monthly_debits, 0) - ISNULL(cmb2.paid, 0) - ISNULL(cmb2.advanced_payment, 0)
                                    ELSE 0
                                END
                            )
                            FROM client_month_balances cmb2
                            WHERE cmb2.rental_id = r.rental_id AND cmb2.id < db.Id
                        ), 0),
                        Raw_Interest = ISNULL((
                            SELECT SUM(ISNULL(cmb2.interests, 0))
                            FROM client_month_balances cmb2
                            WHERE cmb2.rental_id = r.rental_id AND (cmb2.balance - cmb2.paid - cmb2.advanced_payment) > 0
                        ), 0),
                        TotalPaid = ISNULL(db.PaidDB, 0) + ISNULL(db.AdvPayDB, 0)
                ) rawData

                -- Liquidation cascade
                OUTER APPLY (
                    SELECT Rem1 = CASE WHEN rawData.TotalPaid > rawData.Raw_PrevBal THEN rawData.TotalPaid - rawData.Raw_PrevBal ELSE 0 END
                ) calc1
                OUTER APPLY (
                    SELECT Rem2 = CASE WHEN calc1.Rem1 > db.RentDB THEN calc1.Rem1 - db.RentDB ELSE 0 END
                ) calc2
                OUTER APPLY (
                    SELECT UnpaidInts = CASE WHEN calc2.Rem2 > rawData.Raw_Interest THEN 0 ELSE rawData.Raw_Interest - calc2.Rem2 END
                ) calc3

                -- Assign to UI fields
                OUTER APPLY (
                    SELECT
                        UI_CurrentRent = CASE
                            WHEN db.MonthYearDB IS NOT NULL THEN
                                ISNULL((
                                    SELECT TOP 1 rah.amount
                                    FROM rental_amount_history rah
                                    WHERE rah.rental_id = r.rental_id
                                      AND rah.start_date <= DATEFROMPARTS(
                                            CAST(RIGHT(db.MonthYearDB, 4) AS INT),
                                            CAST(LEFT(db.MonthYearDB, 2) AS INT), 1)
                                    ORDER BY rah.start_date DESC,
                                             CASE WHEN rah.end_date IS NULL THEN 1 ELSE 0 END DESC,
                                             rah.rental_amount_history_id DESC
                                ), ISNULL(cr.CurrentRent, 0))
                            ELSE ISNULL(cr.CurrentRent, 0)
                        END,
                        UI_InterestAmount = calc3.UnpaidInts,
                        UI_Balance = -(db.PrevBalDB + db.IntsDB + db.RentDB - db.PaidDB - db.AdvPayDB),
                        UI_PreviousBalance = CASE
                            WHEN ISNULL(db.AdvPayDB, 0) > 0 AND ISNULL(db.AdvPayDB, 0) < db.RentDB THEN ISNULL(db.AdvPayDB, 0)
                            ELSE -rawData.Raw_PrevBal
                        END,
                        LastBalanceDate = CASE
                            WHEN latest_cmb.MonthYearDB IS NOT NULL AND LEN(latest_cmb.MonthYearDB) = 7 THEN
                                CASE
                                    WHEN latest_cmb.NetBalance <= 0 THEN
                                        DATEADD(month, 1, DATEFROMPARTS(CAST(RIGHT(latest_cmb.MonthYearDB, 4) AS INT), CAST(LEFT(latest_cmb.MonthYearDB, 2) AS INT), 1))
                                    ELSE
                                        DATEFROMPARTS(CAST(RIGHT(ISNULL(db.MonthYearDB, latest_cmb.MonthYearDB), 4) AS INT), CAST(LEFT(ISNULL(db.MonthYearDB, latest_cmb.MonthYearDB), 2) AS INT), 1)
                                END
                            ELSE NULL
                        END
                ) step1

                WHERE c.active = 1
                ORDER BY c.payment_identifier ASC";

            var clientsTable = await _accessDB.GetTableAsync("clients", clientsQuery);
            foreach (DataRow row in clientsTable.Rows)
            {
                snapshot.Clients.Add(new SyncClientDto
                {
                    Id = Convert.ToInt32(row["Id"]),
                    FullName = row["FullName"]?.ToString() ?? string.Empty,
                    PaymentIdentifier = row["PaymentIdentifier"] != DBNull.Value ? Convert.ToDecimal(row["PaymentIdentifier"]) : null,
                    Balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]) : null,
                    PreviousBalance = row["PreviousBalance"] != DBNull.Value ? Convert.ToDecimal(row["PreviousBalance"]) : null,
                    CurrentRent = row["CurrentRent"] != DBNull.Value ? Convert.ToDecimal(row["CurrentRent"]) : null,
                    PendingSurcharge = row["PendingSurcharge"] != DBNull.Value ? Convert.ToDecimal(row["PendingSurcharge"]) : null,
                    InterestAmount = row["InterestAmount"] != DBNull.Value ? Convert.ToDecimal(row["InterestAmount"]) : null,
                    LastGeneratedMonthYear = row["LastGeneratedMonthYear"]?.ToString(),
                    Color = row["Color"]?.ToString(),
                    Active = row["Active"] != DBNull.Value ? Convert.ToBoolean(row["Active"]) : null,
                    PreferredPaymentMethodId = row["PreferredPaymentMethodId"] != DBNull.Value ? Convert.ToInt32(row["PreferredPaymentMethodId"]) : null,
                    // New enriched fields
                    NextPaymentDay = row["NextPaymentDay"]?.ToString(),
                    Status = row["Status"]?.ToString(),
                    RentalId = row["RentalId"] != DBNull.Value ? Convert.ToInt32(row["RentalId"]) : null,
                    MonthsUnpaid = row["MonthsUnpaid"] != DBNull.Value ? Convert.ToInt32(row["MonthsUnpaid"]) : null,
                    IncreaseAnchorDate = row["IncreaseAnchorDate"] != DBNull.Value ? Convert.ToDateTime(row["IncreaseAnchorDate"]).ToString("yyyy-MM-dd") : null,
                    IncreaseFrequencyMonths = row["IncreaseFrequencyMonths"] != DBNull.Value ? Convert.ToInt32(row["IncreaseFrequencyMonths"]) : null,
                    IsSixMonthPromotion = row["IsSixMonthPromotion"] != DBNull.Value && Convert.ToBoolean(row["IsSixMonthPromotion"]),
                    PlannedPaymentAmount = row["PlannedPaymentAmount"] != DBNull.Value ? Convert.ToDecimal(row["PlannedPaymentAmount"]) : 0m,
                    HasPlannedPayment = row["HasPlannedPayment"] != DBNull.Value && Convert.ToBoolean(row["HasPlannedPayment"]),
                });
            }

            // Load payment methods
            var pmQuery = "SELECT payment_method_id, name, commission FROM payment_methods WHERE active = 1";
            var pmTable = await _accessDB.GetTableAsync("payment_methods", pmQuery);
            foreach (DataRow row in pmTable.Rows)
            {
                snapshot.PaymentMethods.Add(new SyncPaymentMethodDto
                {
                    Id = Convert.ToInt32(row["payment_method_id"]),
                    Name = row["name"]?.ToString() ?? string.Empty,
                    Commission = row["commission"] != DBNull.Value ? Convert.ToDecimal(row["commission"]) : 0
                });
            }

            // Load active rentals
            var rentalsQuery = "SELECT rental_id, client_id, active FROM rentals WHERE active = 1";
            var rentalsTable = await _accessDB.GetTableAsync("rentals", rentalsQuery);
            foreach (DataRow row in rentalsTable.Rows)
            {
                snapshot.Rentals.Add(new SyncRentalDto
                {
                    Id = Convert.ToInt32(row["rental_id"]),
                    ClientId = Convert.ToInt32(row["client_id"]),
                    Active = Convert.ToBoolean(row["active"])
                });
            }

            return snapshot;
        }

        public async Task<SyncPaymentsResponseDto> ProcessOfflinePaymentsAsync(SyncPaymentsRequestDto request)
        {
            var response = new SyncPaymentsResponseDto();

            if (request.Payments == null || request.Payments.Count == 0)
                return response;

            // Sort by date so payments are applied in chronological order
            var orderedPayments = request.Payments.OrderBy(p => p.Date).ToList();

            foreach (var offlinePayment in orderedPayments)
            {
                var result = new SyncPaymentResultDto { LocalId = offlinePayment.LocalId };
                try
                {
                    var dto = new CreatePaymentTransaction
                    {
                        ClientId = offlinePayment.ClientId,
                        PaymentMethodId = offlinePayment.PaymentMethodId,
                        MovementType = offlinePayment.MovementType,
                        Concept = offlinePayment.Concept,
                        Amount = offlinePayment.Amount,
                        Date = offlinePayment.Date,
                        IsAdvancePayment = offlinePayment.IsAdvancePayment,
                        AdvanceMonths = offlinePayment.AdvanceMonths,
                        CommissionAmount = offlinePayment.CommissionAmount,
                        CommissionConcept = offlinePayment.CommissionConcept,
                        SkipFutureProjection = offlinePayment.SkipFutureProjection,
                        SurchargeAction = offlinePayment.SurchargeAction,
                        SurchargeAmount = offlinePayment.SurchargeAmount,
                        ExpectedPaymentStateToken = offlinePayment.ExpectedPaymentStateToken,
                        AppliedIncreases = new List<PaymentIncreaseDto>()
                    };

                    bool success = await _paymentService.CreatePaymentWithMovementAsync(dto);
                    result.Success = success;
                    if (!success)
                        result.ErrorMessage = "El servidor no pudo procesar el pago.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing offline payment with LocalId={LocalId} for ClientId={ClientId}", offlinePayment.LocalId, offlinePayment.ClientId);
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                }

                response.Results.Add(result);
            }

            return response;
        }
    }
}
