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
                
                // Guardamos la renta BASE y preparamos la NUEVA renta (por si hay aumento)
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
            // ¡MAGIA!: Tomamos el número exacto, sin calcular porcentajes
            newRent = dto.NewRentAmount.Value; 

            // Dejamos constancia en el historial de precios
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

                // --- 2. INSERCIÓN DE CRÉDITO Y COMISIONES EN MOVIMIENTOS ---
                await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement { RentalId = rental.Id, PaymentId = paymentId, MovementDate = dto.Date, MovementType = "CREDITO", Concept = dto.Concept ?? "Pago de alquiler", Amount = dto.Amount }, connection, transaction);

                if (dto.CommissionAmount.HasValue && dto.CommissionAmount.Value != 0)
                {
                    await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement { RentalId = rental.Id, PaymentId = paymentId, MovementDate = dto.Date, MovementType = dto.CommissionAmount.Value > 0 ? "DEBITO" : "CREDITO", Concept = dto.CommissionConcept ?? "Ajuste de pago", Amount = Math.Abs(dto.CommissionAmount.Value) }, connection, transaction);
                }

                // ==============================================================================
                // --- 3. LA CASCADA: DISTRIBUCIÓN DEL DINERO Y RECALCULO DE SALDOS ANTERIORES
                // ==============================================================================
                decimal moneyInHand = dto.Amount;
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

                decimal rolledOverDebt = 0;

                for (int i = 0; i < existingMonths.Count; i++)
                {
                    var month = existingMonths[i];

                    if (i > 0) 
                    {
                        month.PreviousBalance = rolledOverDebt;
                        month.Balance = month.PreviousBalance + month.Interests + month.MonthlyDebits;

                        string updBal = "UPDATE client_month_balances SET previous_balance = @pb, balance = @b WHERE id = @id";
                        using var cmdBal = new SqlCommand(updBal, connection, transaction);
                        cmdBal.Parameters.AddWithValue("@pb", month.PreviousBalance);
                        cmdBal.Parameters.AddWithValue("@b", month.Balance);
                        cmdBal.Parameters.AddWithValue("@id", month.Id);
                        await cmdBal.ExecuteNonQueryAsync();
                    }

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

                if (moneyInHand > 0 || lastMonthWasTouched)
                {
                    while (true)
                    {
                        lastGeneratedDate = lastGeneratedDate.AddMonths(1);
                        string newMonthStr = lastGeneratedDate.ToString("MM/yyyy");
                        DateTime currentIterMonth = new DateTime(lastGeneratedDate.Year, lastGeneratedDate.Month, 1);

                        decimal rentForThisMonth = baseRent;
                        if (rental.IncreaseAnchorDate.HasValue)
                        {
                            DateTime anchor = new DateTime(rental.IncreaseAnchorDate.Value.Year, rental.IncreaseAnchorDate.Value.Month, 1);
                            if (currentIterMonth >= anchor)
                            {
                                rentForThisMonth = newRent; 
                            }
                        }

                        var culture = new CultureInfo("es-AR");
                        string monthName = culture.DateTimeFormat.GetMonthName(lastGeneratedDate.Month);
                        string conceptDebit = $"Alquiler {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName)} {lastGeneratedDate.Year}";

                        if (!await accountMovementService.IsDebitAlreadyCreatedAsync(rental.Id, conceptDebit, connection, transaction))
                        {
                            await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement {
                                RentalId = rental.Id, PaymentId = paymentId, MovementDate = dto.Date,
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

                        if (moneyInHand <= 0 && applied == 0) break;
                    }
                }

                // ==============================================================================
                // --- 5. EFECTIVIZACIÓN Y LIMPIEZA DE MORA ---
                // ==============================================================================
                
                if (rental.PendingSurcharge > 0)
                {
                    // Fecha contable: El mes siguiente (para que caiga en la tabla del mes que viene)
                    DateTime nextMonthInterestDate = new DateTime(dto.Date.Year, dto.Date.Month, 1).AddMonths(1);
                    
                    // ¡NUEVO!: Concepto usando el mes actual del pago (ej: Mayo)
                    var culture = new CultureInfo("es-AR");
                    string monthOfDebt = culture.DateTimeFormat.GetMonthName(dto.Date.Month);
                    string monthTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthOfDebt);

                    await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement {
                        RentalId = rental.Id, 
                        PaymentId = paymentId, 
                        MovementDate = nextMonthInterestDate, // <-- Esto lo empuja a la fila de Junio
                        MovementType = "DEBITO", 
                        Concept = $"Interés por mora de {monthTitle} {dto.Date.Year}", // <-- Esto imprime "Mayo 2026"
                        Amount = (decimal)rental.PendingSurcharge
                    }, connection, transaction);
                }

                await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rental.Id, connection, transaction);
                
                await daoRental.ResetPendingSurchargeTransactionAsync(rental.Id, connection, transaction);
                await daoRental.ResetUnpaidMonthsTransactionAsync(rental.Id, connection, transaction);

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
            int? rentalId = null;
            using (var connection = accessDB.GetConnectionClose())
            {
                await connection.OpenAsync();
                const string lookupQuery = "SELECT rental_id FROM account_movements WHERE movement_id = @movement_id";
                using var lookupCommand = new SqlCommand(lookupQuery, connection);
                lookupCommand.Parameters.AddWithValue("@movement_id", movementId);
                var result = await lookupCommand.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    rentalId = Convert.ToInt32(result);
            }

            bool deleted = await _daoPayment.DeletePaymentTransactionAsync(movementId);
            if (deleted && rentalId.HasValue)
                await _clientMonthBalanceService.RebuildForRentalAsync(rentalId.Value);

			return deleted;
		}

		private decimal RoundToNearest1000(decimal amount)
		{
			if (amount == 0) return 0;
			return Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
		}

	}
}
