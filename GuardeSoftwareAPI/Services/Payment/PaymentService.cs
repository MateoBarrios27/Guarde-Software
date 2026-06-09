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

		public PaymentService(AccessDB _accessDB, IAccountMovementService _accountMovementService, ILogger<PaymentService> logger, IRentalService _rentalService, IRentalAmountHistoryService _rentalAmountHistoryService, IPaymentMethodService _paymentMethodService, IClientMonthBalanceService clientMonthBalanceService)
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


		public async Task<bool> CreatePaymentWithMovementAsync(CreatePaymentTransaction dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "DTO cannot be null.");
            if (dto.ClientId <= 0) throw new ArgumentException("Invalid client ID.");
            if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than 0.");

            using var connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
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
                bool isPriceLocked = dto.IsAdvancePayment && monthsToCover >= 6;
                if (isPriceLocked)
                {
                    DateTime lockEndDate = dto.Date.Date.AddMonths(monthsToCover);
                    if (!rental.PriceLockEndDate.HasValue || lockEndDate > rental.PriceLockEndDate.Value.Date)
                        await daoRental.UpdatePriceLockEndDateTransactionAsync(rental.Id, lockEndDate, connection, transaction);
                }
                else if (dto.NewRentAmount.HasValue && dto.NewRentAmount.Value > baseRent)
                {
                    // Se crea el historial nuevo
                    newRent = dto.NewRentAmount.Value; 

                    var lastHistory = await rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(rental.Id, connection, transaction);
                    if (lastHistory != null)
                    {
                        DateTime effectiveDate = rental.IncreaseAnchorDate ?? dto.Date;
                        await rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(lastHistory.Id, rental.Id, newRent, effectiveDate, connection, transaction);
                    }

                    string updateAnchorQuery = @"
                        UPDATE rentals 
                        SET increase_anchor_date = DATEADD(month, (SELECT increase_frequency_months - 1 FROM clients WHERE client_id = @clientId), increase_anchor_date)
                        WHERE rental_id = @rentalId AND increase_anchor_date IS NOT NULL";
                    
                    using var cmdAnchor = new SqlCommand(updateAnchorQuery, connection, transaction);
                    cmdAnchor.Parameters.AddWithValue("@clientId", rental.ClientId);
                    cmdAnchor.Parameters.AddWithValue("@rentalId", rental.Id);
                    await cmdAnchor.ExecuteNonQueryAsync();
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

        if (rental.PendingSurcharge > 0)
        {
            // A. OBTENER EL VALOR REAL DE LA DEUDA DEL MES DE MORA
            // Consultamos el monthly_debits del mes exacto en que se originó el pago tardío.
            decimal debtAmountForPenalty = 0;
            string penaltyQuery = @"
                SELECT monthly_debits 
                FROM client_month_balances 
                WHERE rental_id = @rid 
                AND month_year = @mYear";

            using (var cmdPenalty = new SqlCommand(penaltyQuery, connection, transaction))
            {
                cmdPenalty.Parameters.AddWithValue("@rid", rental.Id);
                cmdPenalty.Parameters.AddWithValue("@mYear", dto.Date.ToString("MM/yyyy"));
                var result = await cmdPenalty.ExecuteScalarAsync();
                if (result != null) debtAmountForPenalty = Convert.ToDecimal(result);
            }

            // B. CALCULAR EL 10% SOBRE ESE VALOR ESPECÍFICO
            decimal rawPenalty = debtAmountForPenalty * 0.10m;
            decimal finalPenalty = rawPenalty;

            // C. REDONDEO SEGÚN MÉTODO DE PAGO
            string paymentMethodName = "";
            string pmQuery = "SELECT name FROM payment_methods WHERE payment_method_id = (SELECT preferred_payment_method_id FROM clients WHERE client_id = @cid)";
            using (var cmdPm = new SqlCommand(pmQuery, connection, transaction))
            {
                cmdPm.Parameters.AddWithValue("@cid", rental.ClientId);
                var result = await cmdPm.ExecuteScalarAsync();
                if (result != null) paymentMethodName = result.ToString().ToLower();
            }

            if (paymentMethodName.Contains("efectivo")) 
            {
                finalPenalty = Math.Round(rawPenalty / 1000m, MidpointRounding.AwayFromZero) * 1000m;
            } 
            else 
            {
                finalPenalty = Math.Round(rawPenalty / 100m, MidpointRounding.AwayFromZero) * 100m;
            }

            // D. CREAR EL DÉBITO DEL INTERÉS
            DateTime nextMonthInterestDate = new DateTime(dto.Date.Year, dto.Date.Month, 1).AddMonths(1);
            string monthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(new CultureInfo("es-AR").DateTimeFormat.GetMonthName(dto.Date.Month));

            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement {
                RentalId = rental.Id, 
                PaymentId = paymentId, 
                MovementDate = nextMonthInterestDate, 
                MovementType = "DEBITO", 
                Concept = $"Interés por mora de {monthTitle} {dto.Date.Year}", 
                Amount = finalPenalty 
            }, connection, transaction);
        }

        // E. LIMPIEZA Y RECONSTRUCCIÓN (SIEMPRE EJECUTAR)
        await daoRental.ResetPendingSurchargeTransactionAsync(rental.Id, connection, transaction);
        await daoRental.ResetUnpaidMonthsTransactionAsync(rental.Id, connection, transaction);
        await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rental.Id, connection, transaction);

        await transaction.CommitAsync();
        return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
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

                const string lookupQuery = @"
                    SELECT am.rental_id, p.payment_date
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
                    }
                }

                bool deleted = await _daoPayment.DeletePaymentTransactionAsync(movementId, connection, transaction);
                if (!deleted)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                if (rentalId.HasValue)
                {
                    if (paymentDate.HasValue)
                        await RestoreLatestRentChangeIfNeededAsync(rentalId.Value, paymentDate.Value, connection, transaction);

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

        private async Task RestoreLatestRentChangeIfNeededAsync(int rentalId, DateTime paymentDate, SqlConnection connection, SqlTransaction transaction)
        {
            const string historiesQuery = @"
                SELECT TOP 2 rental_amount_history_id, rental_id, amount, start_date, end_date
                FROM rental_amount_history
                WHERE rental_id = @rental_id
                ORDER BY start_date DESC, rental_amount_history_id DESC;";

            var histories = new List<RentalAmountHistory>();
            using (var command = new SqlCommand(historiesQuery, connection, transaction))
            {
                command.Parameters.AddWithValue("@rental_id", rentalId);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    histories.Add(new RentalAmountHistory
                    {
                        Id = reader.GetInt32(0),
                        RentalId = reader.GetInt32(1),
                        Amount = reader.GetDecimal(2),
                        StartDate = reader.GetDateTime(3),
                        EndDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                    });
                }
            }

            if (histories.Count < 2) return;

            var latest = histories[0];
            var previous = histories[1];

            if (latest.StartDate.Date < paymentDate.Date)
                return;

            const string deleteLatestQuery = "DELETE FROM rental_amount_history WHERE rental_amount_history_id = @history_id";
            using (var deleteCommand = new SqlCommand(deleteLatestQuery, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("@history_id", latest.Id);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            const string reopenPreviousQuery = "UPDATE rental_amount_history SET end_date = NULL WHERE rental_amount_history_id = @history_id";
            using (var reopenCommand = new SqlCommand(reopenPreviousQuery, connection, transaction))
            {
                reopenCommand.Parameters.AddWithValue("@history_id", previous.Id);
                await reopenCommand.ExecuteNonQueryAsync();
            }

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

            if (frequencyMonths > 0)
            {
                var restoredAnchorDate = previous.StartDate.Date.AddMonths(frequencyMonths);
                const string updateAnchorQuery = "UPDATE rentals SET increase_anchor_date = @anchor_date WHERE rental_id = @rental_id";
                using var updateCommand = new SqlCommand(updateAnchorQuery, connection, transaction);
                updateCommand.Parameters.AddWithValue("@anchor_date", restoredAnchorDate);
                updateCommand.Parameters.AddWithValue("@rental_id", rentalId);
                await updateCommand.ExecuteNonQueryAsync();
            }
        }

		private decimal RoundToNearest1000(decimal amount)
		{
			if (amount == 0) return 0;
			return Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
		}

	}
}
