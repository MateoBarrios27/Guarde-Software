using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Services.accountMovement;
using GuardeSoftwareAPI.Services.rental;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using GuardeSoftwareAPI.Services.paymentMethod;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Hubs;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace GuardeSoftwareAPI.Services.payment
{

	public class PaymentService : IPaymentService
	{
		private readonly DaoPayment _daoPayment;
		private readonly IAccountMovementService accountMovementService;
		private readonly IRentalService rentalService;
		private readonly ILogger<PaymentService> logger;
		private readonly DaoRental daoRental;
		private readonly IRentalAmountHistoryService rentalAmountHistoryService;
		private readonly IPaymentMethodService paymentMethodService;
		private readonly DaoClientMonthBalance _daoMonthBalance;
		private readonly AccessDB accessDB;
        private readonly IClientMonthBalanceService _clientMonthBalanceService;
        private readonly IPaymentStateService _paymentStateService;
        private readonly PaymentPresenceRegistry _paymentPresenceRegistry;

		public PaymentService(AccessDB _accessDB, IAccountMovementService _accountMovementService, ILogger<PaymentService> logger, IRentalService _rentalService, IRentalAmountHistoryService _rentalAmountHistoryService, IPaymentMethodService _paymentMethodService, IClientMonthBalanceService clientMonthBalanceService, IPaymentStateService paymentStateService, PaymentPresenceRegistry paymentPresenceRegistry)
		{
			this._daoPayment = new DaoPayment(_accessDB);
			this.accountMovementService = _accountMovementService;
			this.accessDB = _accessDB;
			this.daoRental = new DaoRental(_accessDB);
			this.logger = logger;
			this.paymentMethodService = _paymentMethodService;
			this.rentalService = _rentalService;
			this.rentalAmountHistoryService = _rentalAmountHistoryService;
			this._daoMonthBalance = new DaoClientMonthBalance(_accessDB);
             _clientMonthBalanceService = clientMonthBalanceService;
             _paymentStateService = paymentStateService;
             _paymentPresenceRegistry = paymentPresenceRegistry;
		}

		public async Task<List<Payment>> GetPaymentsList()
		{
			DataTable paymentTable = await _daoPayment.GetPayments();
			List<Payment> payments = [];

			if (paymentTable.Rows.Count == 0) throw new ArgumentException("No payments found.");

			foreach (DataRow row in paymentTable.Rows)
			{
				int paymentId = (int)row["payment_id"];

				Payment payment = new()
                {
					Id = paymentId,
					Amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0m,
					PaymentDate = row["payment_date"] != DBNull.Value ? (DateTime)row["payment_date"] : DateTime.MinValue,
					PaymentMethodId = row["payment_method_id"] != DBNull.Value ? (int)row["payment_method_id"] : 0,
					ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
					ClientName = row["full_name"]?.ToString() ?? string.Empty,
					PaymentIdentifier = row["payment_identifier"] != DBNull.Value ? Convert.ToDecimal(row["payment_identifier"]) : 0m,
				};

				payments.Add(payment);
			}

			return payments;
		}

		public async Task<Payment> GetPaymentById(int id)
		{
			if (id <= 0) throw new ArgumentException("Invalid payment ID.");

			DataTable paymentTable = await _daoPayment.GetPaymentById(id);

			if (paymentTable.Rows.Count == 0) throw new ArgumentException("No payment found with the given ID.");

			DataRow row = paymentTable.Rows[0];

			return new Payment
			{
				Id = (int)row["payment_id"],
				Amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0m,
				PaymentDate = row["payment_date"] != DBNull.Value ? (DateTime)row["payment_date"] : DateTime.MinValue,
				PaymentMethodId = row["payment_method_id"] != DBNull.Value ? (int)row["payment_method_id"] : 0,
				ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
			};
		}

		public async Task<List<Payment>> GetPaymentsByClientId(int clientId)
		{
			if (clientId <= 0) throw new ArgumentException("The client ID must be a positive integer.");

			DataTable paymentTable = await _daoPayment.GetPaymentsByClientId(clientId);
			List<Payment> payments = new List<Payment>();

			foreach (DataRow row in paymentTable.Rows)
			{
				Payment payment = new Payment
				{
					Id = row["payment_id"] != DBNull.Value ? (int)row["payment_id"] : 0,
					ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0,
					PaymentMethodId = row["payment_method_id"] != DBNull.Value ? (int)row["payment_method_id"] : 0,
					PaymentDate = row["payment_date"] != DBNull.Value ? (DateTime)row["payment_date"] : DateTime.MinValue,
					Amount = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0m
				};

				payments.Add(payment);
			}

			return payments;
		}


		public async Task<bool> CreatePaymentWithMovementAsync(CreatePaymentTransaction dto, string? recordedByName = null, string? recordedByUserName = null)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "DTO cannot be null.");
            if (dto.ClientId <= 0) throw new ArgumentException("Invalid client ID.");
            if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than 0.");

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                await AcquireClientPaymentLockAsync(dto.ClientId, connection, transaction);

                var currentState = await _paymentStateService.GetSnapshotAsync(dto.ClientId, connection, transaction);
                if (!string.IsNullOrWhiteSpace(dto.ExpectedPaymentStateToken)
                    && !string.Equals(dto.ExpectedPaymentStateToken, currentState.Token, StringComparison.Ordinal))
                {
                    throw new PaymentConflictException(await BuildConflictDetailsAsync(dto, connection, transaction, duplicateAttempt: false));
                }

                if (string.IsNullOrWhiteSpace(dto.ExpectedPaymentStateToken)
                    && await IsRecentDuplicateByConceptAsync(dto, connection, transaction))
                {
                    throw new PaymentConflictException(await BuildConflictDetailsAsync(dto, connection, transaction, duplicateAttempt: true));
                }

                // 1. OBTENEMOS DATOS BASE Y CREAMOS EL PAGO GENERAL
                int paymentId = await _daoPayment.CreatePaymentTransactionAsync(new Payment
                {
                    ClientId = dto.ClientId, PaymentMethodId = dto.PaymentMethodId, Amount = dto.Amount, PaymentDate = dto.Date
                }, connection, transaction);

                var rental = await rentalService.GetRentalByClientIdTransactionAsync(dto.ClientId, connection, transaction);
                if (rental == null) throw new Exception("El cliente no tiene alquiler activo");

                int monthsToCover = (dto.IsAdvancePayment && dto.AdvanceMonths.HasValue && dto.AdvanceMonths.Value > 0) ? dto.AdvanceMonths.Value : 1;
                
                decimal baseRent = (decimal)rental.CurrentAmount;
                decimal newRent = baseRent;

                // --- LÓGICA DE AUMENTO Y CONGELAMIENTO ---
                // Un pago de 6+ meses congela el precio. También respetamos un congelamiento
                // ya creado al planificar previamente esos débitos, aunque el pago se cargue después.
                bool hasActivePlannedPriceLock = rental.PriceLockEndDate.HasValue
                    && rental.PriceLockEndDate.Value.Date > dto.Date.Date;
                bool isPriceLocked = (dto.IsAdvancePayment && monthsToCover >= 6) || hasActivePlannedPriceLock;
                if (isPriceLocked)
                {
                    DateTime lockEndDate = dto.Date.Date.AddMonths(monthsToCover);
                    if (!rental.PriceLockEndDate.HasValue || lockEndDate > rental.PriceLockEndDate.Value.Date)
                        await daoRental.UpdatePriceLockEndDateTransactionAsync(rental.Id, lockEndDate, connection, transaction);
                }
                else if (dto.AppliedIncreases != null && dto.AppliedIncreases.Any())
                {
                    // Procesamos la cola de aumentos en orden cronológico
                    foreach(var inc in dto.AppliedIncreases.OrderBy(x => x.Year).ThenBy(x => x.Month))
                    {
                        DateTime effectiveDate = new DateTime(inc.Year, inc.Month, 1);
                        decimal rentEscalonada = inc.NewRentAmount;

                        var lastHistory = await rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(rental.Id, connection, transaction);
                        if (lastHistory != null)
                        {
                            await rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(lastHistory.Id, rental.Id, rentEscalonada, effectiveDate, connection, transaction);
                        }

                        // Avanzamos el ancla un escalón por cada aumento detectado
                        // (Mantenemos la lógica de 'increase_frequency_months - 1' para respetar los ciclos)
                        string updateAnchorQuery = @"
                            UPDATE rentals 
                            SET increase_anchor_date = DATEADD(month, (SELECT increase_frequency_months - 1 FROM clients WHERE client_id = @clientId), increase_anchor_date)
                            WHERE rental_id = @rentalId AND increase_anchor_date IS NOT NULL";
                        
                        using var cmdAnchor = new SqlCommand(updateAnchorQuery, connection, transaction);
                        cmdAnchor.Parameters.AddWithValue("@clientId", rental.ClientId);
                        cmdAnchor.Parameters.AddWithValue("@rentalId", rental.Id);
                        await cmdAnchor.ExecuteNonQueryAsync();
                    }
                }

                // ==============================================================================
                // --- NUEVO: EXTRAER HISTORIAL ACTUALIZADO PARA RESOLVER PRECIOS ---
                // Leemos el historial de la DB (incluso si acabamos de insertar uno nuevo por aumento)
                // ==============================================================================
                var histories = new List<RentalAmountHistory>();
                string histQuery = "SELECT amount, start_date, end_date FROM rental_amount_history WHERE rental_id = @rid";
                using (var cmdHist = new SqlCommand(histQuery, connection, transaction))
                {
                    cmdHist.Parameters.AddWithValue("@rid", rental.Id);
                    using (var reader = await cmdHist.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            histories.Add(new RentalAmountHistory {
                                Amount = reader.GetDecimal(0),
                                StartDate = reader.GetDateTime(1),
                                EndDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2)
                            });
                        }
                    }
                }

                // --- 2. INSERCIÓN DE CRÉDITO Y COMISIONES EN MOVIMIENTOS ---
                await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement { RentalId = rental.Id, PaymentId = paymentId, MovementDate = dto.Date, MovementType = "CREDITO", Concept = dto.Concept ?? "Pago de alquiler", Amount = dto.Amount }, connection, transaction);

                if (dto.CommissionAmount.HasValue && dto.CommissionAmount.Value != 0)
                {
                    await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement { RentalId = rental.Id, PaymentId = paymentId, MovementDate = dto.Date, MovementType = dto.CommissionAmount.Value > 0 ? "DEBITO" : "CREDITO", Concept = dto.CommissionConcept ?? "Ajuste de pago", Amount = Math.Abs(dto.CommissionAmount.Value) }, connection, transaction);
                }

                decimal moneyInHand = dto.Amount;
                
                if (dto.CommissionAmount.HasValue && dto.CommissionAmount.Value != 0)
                {
                    moneyInHand -= dto.CommissionAmount.Value;
                }

                DateTime currentRealMonth = new DateTime(dto.Date.Year, dto.Date.Month, 1);

                var existingMonths = new List<ClientMonthBalance>();
                string selectQuery = "SELECT id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment FROM client_month_balances WHERE rental_id = @rental_id ORDER BY id ASC";
                using (var cmdSelect = new SqlCommand(selectQuery, connection, transaction))
                {
                    cmdSelect.Parameters.AddWithValue("@rental_id", rental.Id);
                    using (var reader = await cmdSelect.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            existingMonths.Add(new ClientMonthBalance {
                                Id = reader.GetInt32(0), MonthYear = reader.GetString(1), PreviousBalance = reader.GetDecimal(2),
                                Interests = reader.GetDecimal(3), MonthlyDebits = reader.GetDecimal(4), Balance = reader.GetDecimal(5),
                                Paid = reader.GetDecimal(6), AdvancedPayment = reader.GetDecimal(7)
                            });
                        }
                    }
                }

                // ==============================================================================
                // FIX: CORRECCIÓN RETROACTIVA DE MESES YA EMITIDOS (Comparando solo Año y Mes)
                // ==============================================================================
                if (dto.NewRentAmount.HasValue && dto.NewRentAmount.Value > baseRent)
                {
                    DateTime effectiveDate = rental.IncreaseAnchorDate ?? dto.Date;
                    int effectiveMonthValue = effectiveDate.Year * 100 + effectiveDate.Month;
                    
                    foreach (var month in existingMonths)
                    {
                        DateTime monthDate = DateTime.ParseExact(month.MonthYear, "MM/yyyy", null);
                        int iterMonthValue = monthDate.Year * 100 + monthDate.Month;
                        
                        // FIX: Comparamos YYYYMM >= YYYYMM, ignorando el día exacto del mes
                        if (iterMonthValue >= effectiveMonthValue)
                        {
                            if (month.MonthlyDebits < dto.NewRentAmount.Value)
                            {
                                month.MonthlyDebits = dto.NewRentAmount.Value;
                                
                                var culture = new CultureInfo("es-AR");
                                string monthName = culture.DateTimeFormat.GetMonthName(monthDate.Month);
                                string conceptDebit = $"Alquiler {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName)} {monthDate.Year}";

                                string updateMov = "UPDATE account_movements SET amount = @newAmount WHERE rental_id = @rid AND concept = @concept AND movement_type = 'DEBITO'";
                                using var cmdMov = new SqlCommand(updateMov, connection, transaction);
                                cmdMov.Parameters.AddWithValue("@newAmount", dto.NewRentAmount.Value);
                                cmdMov.Parameters.AddWithValue("@rid", rental.Id);
                                cmdMov.Parameters.AddWithValue("@concept", conceptDebit);
                                await cmdMov.ExecuteNonQueryAsync();
                                
                                string updCmb = "UPDATE client_month_balances SET monthly_debits = @nd WHERE id = @id";
                                using var cmdCmb = new SqlCommand(updCmb, connection, transaction);
                                cmdCmb.Parameters.AddWithValue("@nd", dto.NewRentAmount.Value);
                                cmdCmb.Parameters.AddWithValue("@id", month.Id);
                                await cmdCmb.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                decimal rolledOverDebt = 0;

                for (int i = 0; i < existingMonths.Count; i++)
                {
                    var month = existingMonths[i];

                    if (i > 0) 
                    {
                        month.PreviousBalance = rolledOverDebt;
                    }

                    // FIX: Siempre recalculamos el balance, incluso para el primer mes, 
                    // por si acaba de ser actualizado en la corrección retroactiva de arriba.
                    month.Balance = month.PreviousBalance + month.Interests + month.MonthlyDebits;

                    string updBal = "UPDATE client_month_balances SET previous_balance = @pb, balance = @b WHERE id = @id";
                    using var cmdBal = new SqlCommand(updBal, connection, transaction);
                    cmdBal.Parameters.AddWithValue("@pb", month.PreviousBalance);
                    cmdBal.Parameters.AddWithValue("@b", month.Balance);
                    cmdBal.Parameters.AddWithValue("@id", month.Id);
                    await cmdBal.ExecuteNonQueryAsync();

                    decimal owes = month.Balance - (month.Paid + month.AdvancedPayment);

                    if (owes > 0 && moneyInHand > 0)
                    {
                        decimal applied = Math.Min(moneyInHand, owes);
                        DateTime rowMonth = DateTime.ParseExact(month.MonthYear, "MM/yyyy", null);
                        
                        string colToUpdate = (rowMonth > currentRealMonth) ? "advanced_payment" : "paid";
                        
                        string updPaid = $"UPDATE client_month_balances SET {colToUpdate} = {colToUpdate} + @app WHERE id = @id";
                        using var cmdPaid = new SqlCommand(updPaid, connection, transaction);
                        cmdPaid.Parameters.AddWithValue("@app", applied);
                        cmdPaid.Parameters.AddWithValue("@id", month.Id);
                        await cmdPaid.ExecuteNonQueryAsync();

                        if (colToUpdate == "paid") month.Paid += applied; else month.AdvancedPayment += applied;
                        moneyInHand -= applied;
                    }

                    rolledOverDebt = month.Balance - (month.Paid + month.AdvancedPayment);
                }

                // ==============================================================================
                // --- 4. GENERAMOS EL FUTURO (Adelantos o Proyección de Próximo Pago)
                // ==============================================================================
                string lastMonthStr = existingMonths.Last().MonthYear;
                DateTime lastGeneratedDate = DateTime.ParseExact(lastMonthStr, "MM/yyyy", null);
                decimal lastMonthDebt = rolledOverDebt;

                var lastExistingMonth = existingMonths.Last();
                bool lastMonthWasTouched = (lastExistingMonth.Paid + lastExistingMonth.AdvancedPayment) > 0;

                // FIX: El futuro se deja de proyectar ÚNICAMENTE si el precio está congelado (6 meses o más)
                // Usamos la variable isPriceLocked que ya tenés definida arriba.
                bool shouldProjectFuture = lastMonthWasTouched && !dto.SkipFutureProjection && !isPriceLocked;

                if (moneyInHand > 0 || shouldProjectFuture)
                {
                    while (true)
                    {
                        lastGeneratedDate = lastGeneratedDate.AddMonths(1);
                        string newMonthStr = lastGeneratedDate.ToString("MM/yyyy");
                        DateTime currentIterMonth = new DateTime(lastGeneratedDate.Year, lastGeneratedDate.Month, 1);
                        int currentIterValue = lastGeneratedDate.Year * 100 + lastGeneratedDate.Month;

                        // Filtramos el historial usando Año y Mes (YYYYMM <= YYYYMM)
                        var historyForMonth = histories
                            .Where(h => (h.StartDate.Year * 100 + h.StartDate.Month) <= currentIterValue && 
                                        (!h.EndDate.HasValue || (h.EndDate.Value.Year * 100 + h.EndDate.Value.Month) >= currentIterValue))
                            .OrderByDescending(h => h.StartDate)
                            .FirstOrDefault();

                        decimal rentForThisMonth = historyForMonth != null ? historyForMonth.Amount : baseRent;

                        var culture = new CultureInfo("es-AR");
                        string monthName = culture.DateTimeFormat.GetMonthName(lastGeneratedDate.Month);
                        string conceptDebit = $"Alquiler {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName)} {lastGeneratedDate.Year}";

                        if (!await accountMovementService.IsDebitAlreadyCreatedAsync(rental.Id, conceptDebit, connection, transaction))
                        {
                            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement {
                                RentalId = rental.Id, PaymentId = paymentId, 
                                MovementDate = currentIterMonth, 
                                MovementType = "DEBITO", Concept = conceptDebit, Amount = rentForThisMonth
                            }, connection, transaction);
                        }

                        decimal prevBalForThisNewMonth = lastMonthDebt > 0 ? lastMonthDebt : 0m;
                        decimal intsForThisNewMonth = 0m; 
                        decimal totalOwedThisNewMonth = prevBalForThisNewMonth + intsForThisNewMonth + rentForThisMonth;

                        decimal bucketSize = Math.Max(rentForThisMonth, totalOwedThisNewMonth);
                        decimal applied = Math.Min(moneyInHand, bucketSize);

                        await _daoMonthBalance.CreateMonthBalanceTransactionAsync(new ClientMonthBalance {
                            RentalId = rental.Id,
                            MonthYear = newMonthStr,
                            PreviousBalance = prevBalForThisNewMonth,
                            Interests = intsForThisNewMonth,
                            MonthlyDebits = rentForThisMonth,
                            Paid = 0m,
                            AdvancedPayment = applied
                        }, connection, transaction);

                        moneyInHand -= applied;
                        lastMonthDebt = totalOwedThisNewMonth - applied;

                        // CONDICIONES DE CORTE DEL BUCLE:
                        if (moneyInHand <= 0)
                        {
                            // 1. Si acaba de generar un mes extra donde metió $0, significa que ya proyectó el mes impago. Cortamos.
                            if (applied == 0) break;
                            
                            // 2. Si el usuario pidió omitir.
                            if (dto.SkipFutureProjection) break;
                            
                            // 3. FIX CRÍTICO: Solo cortamos "en seco" (sin generar el mes vacío) SI pagó 6 o más meses (isPriceLocked).
                            if (isPriceLocked) break; 
                        }
                    }
                }
        // ==============================================================================
        // --- 5. EFECTIVIZACIÓN Y LIMPIEZA DE MORA ---
        // ==============================================================================

        string surchargeAction = dto.SurchargeAction ?? (rental.PendingSurcharge > 0 ? "next_payment" : null);

        if (surchargeAction == "forgive")
        {
            // No crear DEBITO de interés, solo limpiar pendiente de mora
        }
        else if (surchargeAction == "immediate" || surchargeAction == "next_payment")
        {
            decimal finalPenalty = dto.SurchargeAmount ?? 0;

            if (finalPenalty <= 0 && rental.PendingSurcharge > 0)
            {
                // Fallback: usar directamente el recargo que ya calculó y guardó el ApplyInterestsJob,
                // asegurando que el monto sea siempre exactamente el mismo.
                finalPenalty = rental.PendingSurcharge.Value;
            }

            if (finalPenalty > 0)
            {
                DateTime interestDate;
                string concept;
                string monthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(new CultureInfo("es-AR").DateTimeFormat.GetMonthName(dto.Date.Month));

                if (surchargeAction == "immediate")
                {
                    interestDate = dto.Date;
                    concept = $"Interés por mora de {monthTitle} {dto.Date.Year} (cobrado en el acto)";
                }
                else
                {
                    interestDate = new DateTime(dto.Date.Year, dto.Date.Month, 1).AddMonths(1);
                    concept = $"Interés por mora de {monthTitle} {dto.Date.Year}";
                }

                await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement {
                    RentalId = rental.Id, 
                    PaymentId = paymentId, 
                    MovementDate = interestDate, 
                    MovementType = "DEBITO", 
                    Concept = concept, 
                    Amount = finalPenalty 
                }, connection, transaction);
            }
        }

        // E. LIMPIEZA Y RECONSTRUCCIÓN (SIEMPRE EJECUTAR)
        await daoRental.ResetPendingSurchargeTransactionAsync(rental.Id, connection, transaction);
        await daoRental.ResetUnpaidMonthsTransactionAsync(rental.Id, connection, transaction);
        await rentalAmountHistoryService.NormalizeRentalAmountHistoryTransactionAsync(rental.Id, connection, transaction);
        await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rental.Id, connection, transaction);

        await transaction.CommitAsync();

        _paymentPresenceRegistry.RecordPayment(new PaymentCompletedNotice
        {
            ClientId = dto.ClientId,
            PayerName = string.IsNullOrWhiteSpace(recordedByName) ? "Otro usuario" : recordedByName,
            PayerUserName = recordedByUserName ?? string.Empty,
            Amount = dto.Amount,
            PaymentDate = dto.Date,
            RecordedAtUtc = DateTime.UtcNow,
            Concept = dto.Concept
        });

        return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task AcquireClientPaymentLockAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                DECLARE @lockResult INT;
                EXEC @lockResult = sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 15000,
                    @DbPrincipal = 'public';
                SELECT @lockResult;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@resource", SqlDbType.NVarChar, 255)
            {
                Value = $"payment-client:{clientId}"
            });

            var result = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (result < 0)
            {
                throw new PaymentConflictException(new PaymentConflictDetails
                {
                    Code = "PAYMENT_IN_PROGRESS",
                    ClientId = clientId,
                    Message = "Hay otra cobranza en proceso para este cliente. Actualiza el estado antes de continuar."
                });
            }
        }

        private async Task<bool> IsRecentDuplicateByConceptAsync(
            CreatePaymentTransaction dto,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            if (string.IsNullOrWhiteSpace(dto.Concept))
            {
                return false;
            }

            const string query = @"
                SELECT TOP (1) 1
                FROM payments p
                INNER JOIN account_movements am ON am.payment_id = p.payment_id AND am.movement_type = 'CREDITO'
                WHERE p.client_id = @clientId
                  AND p.amount = @amount
                  AND UPPER(LTRIM(RTRIM(ISNULL(am.concept, '')))) = UPPER(LTRIM(RTRIM(@concept)))
                  AND ABS(DATEDIFF(MINUTE, p.payment_date, @paymentDate)) <= 1
                ORDER BY p.payment_id DESC;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@clientId", SqlDbType.Int) { Value = dto.ClientId });
            command.Parameters.Add(new SqlParameter("@amount", SqlDbType.Decimal)
            {
                Precision = 18,
                Scale = 2,
                Value = dto.Amount
            });
            command.Parameters.Add(new SqlParameter("@concept", SqlDbType.NVarChar, 255) { Value = dto.Concept.Trim() });
            command.Parameters.Add(new SqlParameter("@paymentDate", SqlDbType.DateTime) { Value = dto.Date });

            return await command.ExecuteScalarAsync() != null;
        }

        private async Task<PaymentConflictDetails> BuildConflictDetailsAsync(
            CreatePaymentTransaction dto,
            SqlConnection connection,
            SqlTransaction transaction,
            bool duplicateAttempt)
        {
            if (!duplicateAttempt)
            {
                return new PaymentConflictDetails
                {
                    Code = "PAYMENT_STATE_CHANGED",
                    ClientId = dto.ClientId,
                    Message = "El estado de cuenta cambio mientras preparabas el pago."
                };
            }

            const string query = @"
                SELECT TOP (1)
                    p.amount,
                    p.payment_date,
                    am.concept
                FROM payments p
                INNER JOIN account_movements am ON am.payment_id = p.payment_id AND am.movement_type = 'CREDITO'
                WHERE p.client_id = @clientId
                  AND p.amount = @amount
                  AND UPPER(LTRIM(RTRIM(ISNULL(am.concept, '')))) = UPPER(LTRIM(RTRIM(@concept)))
                  AND ABS(DATEDIFF(MINUTE, p.payment_date, @paymentDate)) <= 1
                ORDER BY p.payment_id DESC;";

            decimal? amount = null;
            DateTime? registeredAt = null;
            string? concept = null;

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@clientId", SqlDbType.Int) { Value = dto.ClientId });
                command.Parameters.Add(new SqlParameter("@amount", SqlDbType.Decimal)
                {
                    Precision = 18,
                    Scale = 2,
                    Value = dto.Amount
                });
                command.Parameters.Add(new SqlParameter("@concept", SqlDbType.NVarChar, 255) { Value = dto.Concept?.Trim() ?? string.Empty });
                command.Parameters.Add(new SqlParameter("@paymentDate", SqlDbType.DateTime) { Value = dto.Date });
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    amount = reader.IsDBNull(0) ? null : reader.GetDecimal(0);
                    registeredAt = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                    concept = reader.IsDBNull(2) ? null : reader.GetString(2);
                }
            }

            var latestNotice = _paymentPresenceRegistry.GetLatestPayment(dto.ClientId);
            var registryMatchesPayment = latestNotice != null
                && amount.HasValue
                && registeredAt.HasValue
                && latestNotice.Amount == amount.Value
                && Math.Abs((latestNotice.PaymentDate - registeredAt.Value).TotalMinutes) <= 1
                && string.Equals(latestNotice.Concept?.Trim(), concept?.Trim(), StringComparison.OrdinalIgnoreCase);

            return new PaymentConflictDetails
            {
                Code = "DUPLICATE_PAYMENT",
                ClientId = dto.ClientId,
                Message = "Ya se registro un pago con el mismo concepto para este cliente.",
                RegisteredByName = registryMatchesPayment ? latestNotice!.PayerName : null,
                Amount = amount,
                RegisteredAt = registryMatchesPayment ? latestNotice!.RecordedAtUtc : registeredAt,
                Concept = concept,
                SameConcept = amount.HasValue
            };
        }

        private async Task<bool> IsDebitAlreadyCreatedAsync(int id, string targetConcept, SqlConnection connection, SqlTransaction transaction)
        {
            throw new NotImplementedException();
        }

        public async Task<List<DetailedPaymentDto>> GetDetailedPaymentsAsync()
		{
			DataTable table = await _daoPayment.GetDetailedPaymentsAsync();
			List<DetailedPaymentDto> list = [];

			foreach (DataRow row in table.Rows)
			{
				list.Add(new DetailedPaymentDto
				{
					PaymentId = Convert.ToInt32(row["payment_id"]),
					MovementId = Convert.ToInt32(row["movement_id"]),
					ClientId = row["client_id"] != DBNull.Value ? Convert.ToInt32(row["client_id"]) : null,
					ClientName = row["full_name"]?.ToString() ?? string.Empty,
					PaymentIdentifier = row["payment_identifier"]?.ToString() ?? string.Empty,
					Amount = Convert.ToDecimal(row["amount"]),
					PaymentDate = Convert.ToDateTime(row["payment_date"]),
					PaymentMethodName = row["payment_method_name"]?.ToString() ?? string.Empty,
					Concept = row["concept"]?.ToString() ?? string.Empty,
					MovementType = row["movement_type"]?.ToString() ?? string.Empty,
					PreferredPayment = row["preferred_payment_method_id"] != DBNull.Value ? (int)row["preferred_payment_method_id"] : null,
				});
			}

			return list;
		}

		public async Task<bool> DeletePaymentAsync(int movementId)
        {
            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                int? rentalId = null;
                DateTime? paymentDate = null;
                int? paymentId = null;

                const string lookupQuery = @"
                    SELECT am.rental_id, p.payment_date, am.payment_id
                    FROM account_movements am
                    LEFT JOIN payments p ON am.payment_id = p.payment_id
                    WHERE am.movement_id = @movement_id";

                using (var lookupCommand = new SqlCommand(lookupQuery, connection, transaction))
                {
                    lookupCommand.Parameters.AddWithValue("@movement_id", movementId);
                    using var reader = await lookupCommand.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        rentalId = reader["rental_id"] != DBNull.Value ? Convert.ToInt32(reader["rental_id"]) : null;
                        paymentDate = reader["payment_date"] != DBNull.Value ? Convert.ToDateTime(reader["payment_date"]) : null;
                        paymentId = reader["payment_id"] != DBNull.Value ? Convert.ToInt32(reader["payment_id"]) : null;
                    }
                }

                // FIX CRÍTICO: Buscamos cuál fue el primer mes que este pago empezó a cubrir realmente.
                DateTime? minCoverageDate = null;
                if (paymentId.HasValue && paymentId.Value > 0)
                {
                    const string minDateQuery = "SELECT MIN(movement_date) FROM account_movements WHERE payment_id = @pid AND movement_type = 'DEBITO'";
                    using (var minCmd = new SqlCommand(minDateQuery, connection, transaction))
                    {
                        minCmd.Parameters.AddWithValue("@pid", paymentId.Value);
                        var res = await minCmd.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value) minCoverageDate = Convert.ToDateTime(res);
                    }
                }

                // Si por algún motivo no encontramos débitos, caemos en la fecha de pago
                DateTime safeDateToRollback = minCoverageDate ?? paymentDate ?? DateTime.MinValue;

                bool deleted = await _daoPayment.DeletePaymentTransactionAsync(movementId, connection, transaction);
                if (!deleted)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                if (rentalId.HasValue)
                {
                    // El pago puede haber efectivizado meses que ya estaban
                    // planificados. El borrado elimina los movimientos ligados
                    // al pago, pero los débitos planificados que quedaron sin
                    // payment_id siguen definiendo el próximo aumento.
                    bool preservePlannedIncreaseAnchor = await HasPendingPlannedDebitsAsync(
                        rentalId.Value,
                        connection,
                        transaction);

                    // Le pasamos la fecha segura de cobertura, no la fecha en la que apretó el botón de pagar
                    await rentalAmountHistoryService.NormalizeRentalAmountHistoryTransactionAsync(rentalId.Value, connection, transaction);
                    await RestoreLatestRentChangeIfNeededAsync(
                        rentalId.Value,
                        safeDateToRollback,
                        connection,
                        transaction,
                        preservePlannedIncreaseAnchor);
                    await RestorePriceLockAndIncreaseAnchorIfNeededAsync(
                        rentalId.Value,
                        paymentDate ?? safeDateToRollback,
                        connection,
                        transaction);

                    await rentalAmountHistoryService.NormalizeRentalAmountHistoryTransactionAsync(rentalId.Value, connection, transaction);
                    await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rentalId.Value, connection, transaction);
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task RestorePriceLockAndIncreaseAnchorIfNeededAsync(
            int rentalId,
            DateTime paymentDate,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            if (paymentDate == DateTime.MinValue)
                return;

            const string rentalStateQuery = @"
                SELECT
                    r.price_lock_end_date,
                    r.increase_anchor_date,
                    c.increase_frequency_months
                FROM rentals r
                INNER JOIN clients c ON c.client_id = r.client_id
                WHERE r.rental_id = @rental_id";

            DateTime? priceLockEndDate = null;
            DateTime? currentAnchor = null;
            int frequencyMonths = 0;

            using (var stateCommand = new SqlCommand(rentalStateQuery, connection, transaction))
            {
                stateCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                using var reader = await stateCommand.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return;

                priceLockEndDate = reader.IsDBNull(0) ? null : reader.GetDateTime(0);
                currentAnchor = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                frequencyMonths = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            }

            // Si todavía quedan débitos de una planificación, el congelamiento
            // pertenece a esa planificación y no al pago que se está borrando.
            // En ese caso no se debe restaurar la fecha anterior todavía.
            if (await HasPendingPlannedDebitsAsync(rentalId, connection, transaction))
                return;

            // Solo revertimos un congelamiento que seguÃ­a vigente al momento
            // del pago eliminado. Los congelamientos ya vencidos no se tocan.
            if (!priceLockEndDate.HasValue || priceLockEndDate.Value.Date <= paymentDate.Date)
                return;

            DateTime? restoredAnchor = currentAnchor;
            if (frequencyMonths > 1)
            {
                const string latestHistoryQuery = @"
                    SELECT TOP 1 start_date
                    FROM rental_amount_history
                    WHERE rental_id = @rental_id
                    ORDER BY start_date DESC, rental_amount_history_id DESC";

                DateTime? latestHistoryStart = null;
                using (var historyCommand = new SqlCommand(latestHistoryQuery, connection, transaction))
                {
                    historyCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                    var value = await historyCommand.ExecuteScalarAsync();
                    if (value != null && value != DBNull.Value)
                        latestHistoryStart = Convert.ToDateTime(value);
                }

                if (latestHistoryStart.HasValue)
                {
                    var stepMonths = Math.Max(1, frequencyMonths - 1);
                    var historyStart = new DateTime(latestHistoryStart.Value.Year, latestHistoryStart.Value.Month, 1);
                    restoredAnchor = historyStart.AddMonths(stepMonths);
                }
            }

            const string restoreQuery = @"
                UPDATE rentals
                SET price_lock_end_date = NULL,
                    increase_anchor_date = @increase_anchor_date
                WHERE rental_id = @rental_id";

            using var restoreCommand = new SqlCommand(restoreQuery, connection, transaction);
            restoreCommand.Parameters.Add(new SqlParameter("@increase_anchor_date", SqlDbType.Date)
            {
                Value = (object?)restoredAnchor ?? DBNull.Value
            });
            restoreCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            await restoreCommand.ExecuteNonQueryAsync();
        }

        private async Task RestoreLatestRentChangeIfNeededAsync(
            int rentalId,
            DateTime minCoverageDate,
            SqlConnection connection,
            SqlTransaction transaction,
            bool preserveIncreaseAnchor)
        {
            // 1. Obtener la frecuencia de aumento
            int frequencyMonths = 0;
            const string frequencyQuery = @"
                SELECT c.increase_frequency_months
                FROM rentals r
                INNER JOIN clients c ON c.client_id = r.client_id
                WHERE r.rental_id = @rental_id";

            using (var frequencyCommand = new SqlCommand(frequencyQuery, connection, transaction))
            {
                frequencyCommand.Parameters.AddWithValue("@rental_id", rentalId);
                var result = await frequencyCommand.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    frequencyMonths = Convert.ToInt32(result);
            }

            if (frequencyMonths <= 1) return; 
            int stepMonths = frequencyMonths - 1;

            // 2. Obtener el ancla actual
            DateTime? currentAnchor = null;
            const string anchorQuery = "SELECT increase_anchor_date FROM rentals WHERE rental_id = @rental_id";
            using (var cmdAnchor = new SqlCommand(anchorQuery, connection, transaction))
            {
                cmdAnchor.Parameters.AddWithValue("@rental_id", rentalId);
                var result = await cmdAnchor.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    currentAnchor = Convert.ToDateTime(result);
            }

            // Normalizamos la fecha base del pago (ej: 01/03/2026)
            DateTime normalizedMinDate = new DateTime(minCoverageDate.Year, minCoverageDate.Month, 1);

            // 3. BUCLE DE DESENROLLADO (Sin matemáticas frágiles)
            bool keepRollingBack = true;
            while (keepRollingBack)
            {
                const string topHistoryQuery = @"
                    SELECT TOP 1 rental_amount_history_id, start_date 
                    FROM rental_amount_history 
                    WHERE rental_id = @rental_id 
                    ORDER BY start_date DESC, rental_amount_history_id DESC";

                int historyId = 0;
                DateTime startDate = DateTime.MinValue;

                using (var cmdTop = new SqlCommand(topHistoryQuery, connection, transaction))
                {
                    cmdTop.Parameters.AddWithValue("@rental_id", rentalId);
                    using var reader = await cmdTop.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        historyId = reader.GetInt32(0);
                        startDate = reader.GetDateTime(1);
                    }
                }

                // FIX CRÍTICO: Si no hay historial, o el historial es MÁS VIEJO que la base de nuestro pago, frenamos.
                if (historyId == 0 || startDate.Date < normalizedMinDate) break;

                // Las planificaciones mantienen sus propios débitos futuros. Si
                // el pago se elimina después, esos tramos siguen siendo válidos
                // y no deben desaparecer junto con el historial generado por el
                // pago que se está revirtiendo.
                if (startDate.Date > new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
                    && await HasPlannedDebitForHistoryRangeAsync(rentalId, startDate, connection, transaction))
                    break;

                // Extrema seguridad: nunca borramos el primer historial del contrato (la base)
                int historyCount = 0;
                const string countQuery = "SELECT COUNT(1) FROM rental_amount_history WHERE rental_id = @rental_id";
                using (var cmdCount = new SqlCommand(countQuery, connection, transaction))
                {
                    cmdCount.Parameters.AddWithValue("@rental_id", rentalId);
                    historyCount = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());
                }
                if (historyCount <= 1) break; 

                // A. Borramos el historial del tope
                const string deleteQuery = "DELETE FROM rental_amount_history WHERE rental_amount_history_id = @id";
                using (var cmdDel = new SqlCommand(deleteQuery, connection, transaction))
                {
                    cmdDel.Parameters.AddWithValue("@id", historyId);
                    await cmdDel.ExecuteNonQueryAsync();
                }

                // B. Retrocedemos el ancla exactamente 1 escalón por historial borrado
                if (!preserveIncreaseAnchor && currentAnchor.HasValue)
                {
                    currentAnchor = currentAnchor.Value.AddMonths(-stepMonths);
                    const string updateAnchorQuery = "UPDATE rentals SET increase_anchor_date = @newAnchor WHERE rental_id = @rental_id";
                    using (var cmdUpdAnchor = new SqlCommand(updateAnchorQuery, connection, transaction))
                    {
                        cmdUpdAnchor.Parameters.AddWithValue("@newAnchor", currentAnchor.Value);
                        cmdUpdAnchor.Parameters.AddWithValue("@rental_id", rentalId);
                        await cmdUpdAnchor.ExecuteNonQueryAsync();
                    }
                }
            }

            // 4. Reabrir el historial que quedó en el tope
            const string getFinalTopQuery = @"
                SELECT TOP 1 rental_amount_history_id 
                FROM rental_amount_history 
                WHERE rental_id = @rental_id 
                ORDER BY start_date DESC, rental_amount_history_id DESC";

            int finalTopId = 0;
            using (var cmdFinalTop = new SqlCommand(getFinalTopQuery, connection, transaction))
            {
                cmdFinalTop.Parameters.AddWithValue("@rental_id", rentalId);
                var res = await cmdFinalTop.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value) finalTopId = Convert.ToInt32(res);
            }

            if (finalTopId > 0)
            {
                const string reopenQuery = "UPDATE rental_amount_history SET end_date = NULL WHERE rental_amount_history_id = @id";
                using (var cmdReopen = new SqlCommand(reopenQuery, connection, transaction))
                {
                    cmdReopen.Parameters.AddWithValue("@id", finalTopId);
                    await cmdReopen.ExecuteNonQueryAsync();
                }
            }
        }

        private static async Task<bool> HasPlannedDebitForHistoryRangeAsync(
            int rentalId,
            DateTime historyStart,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM account_movements am
                    WHERE am.rental_id = @rental_id
                      AND am.movement_type = 'DEBITO'
                      AND am.payment_id IS NULL
                      AND am.concept LIKE 'Alquiler %'
                      AND DATEFROMPARTS(YEAR(am.movement_date), MONTH(am.movement_date), 1) > DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
                      AND DATEFROMPARTS(YEAR(am.movement_date), MONTH(am.movement_date), 1) >= DATEFROMPARTS(YEAR(@history_start), MONTH(@history_start), 1)
                      AND DATEFROMPARTS(YEAR(am.movement_date), MONTH(am.movement_date), 1) < COALESCE((
                          SELECT MIN(DATEFROMPARTS(YEAR(next_history.start_date), MONTH(next_history.start_date), 1))
                          FROM rental_amount_history next_history
                          WHERE next_history.rental_id = @rental_id
                            AND next_history.start_date > @history_start
                      ), DATEFROMPARTS(9999, 12, 1))
                ) THEN 1 ELSE 0 END";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            command.Parameters.Add(new SqlParameter("@history_start", SqlDbType.DateTime) { Value = historyStart });
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        private static async Task<bool> HasPendingPlannedDebitsAsync(
            int rentalId,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM account_movements
                    WHERE rental_id = @rental_id
                      AND movement_type = 'DEBITO'
                      AND payment_id IS NULL
                      AND concept LIKE 'Alquiler %'
                      AND DATEFROMPARTS(YEAR(movement_date), MONTH(movement_date), 1) > DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
                ) THEN 1 ELSE 0 END";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

		private decimal RoundToNearest1000(decimal amount)
		{
			if (amount == 0) return 0;
			return Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
		}

	}
}
