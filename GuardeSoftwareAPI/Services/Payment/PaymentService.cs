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

		public PaymentService(AccessDB _accessDB, IAccountMovementService _accountMovementService, ILogger<PaymentService> logger, IRentalService _rentalService, IRentalAmountHistoryService _rentalAmountHistoryService, IPaymentMethodService _paymentMethodService)
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
		}

		public async Task<List<Payment>> GetPaymentsList()
		{
			DataTable paymentTable = await _daoPayment.GetPayments();
			List<Payment> payments = new List<Payment>();

			if (paymentTable.Rows.Count == 0) throw new ArgumentException("No payments found.");

			foreach (DataRow row in paymentTable.Rows)
			{
				int paymentId = (int)row["payment_id"];

				Payment payment = new Payment
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
    if (dto.Amount < 0) throw new ArgumentException("Amount must be greater than 0.");

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
        decimal currentRent = (decimal)rental.CurrentAmount;

        // --- LÓGICA DE AUMENTO Y CONGELAMIENTO ---
        bool isPriceLocked = dto.IsAdvancePayment && monthsToCover >= 6;
        if (isPriceLocked)
        {
            DateTime lockEndDate = dto.Date.Date.AddMonths(monthsToCover);
            if (!rental.PriceLockEndDate.HasValue || lockEndDate > rental.PriceLockEndDate.Value.Date)
                await daoRental.UpdatePriceLockEndDateTransactionAsync(rental.Id, lockEndDate, connection, transaction);
        }
        else if (dto.IncreasePercentage.HasValue && dto.IncreasePercentage.Value > 0)
        {
            currentRent = currentRent + (currentRent * (dto.IncreasePercentage.Value / 100m));
            int clientPaymentMethodId = await paymentMethodService.GetPaymentMethodIdByClientId(rental.ClientId);
            if (clientPaymentMethodId == 1) currentRent = RoundToNearest1000(currentRent); 

            var lastHistory = await rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(rental.Id, connection, transaction);
            if (lastHistory != null)
                await rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(lastHistory.Id, rental.Id, currentRent, dto.Date, connection, transaction);
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

            // A. Recalculamos el Saldo Anterior: Si el mes pasado se saldó, la deuda se vuelve 0 en cascada.
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

            // B. ¿Cuánto debe realmente este mes?
            decimal owes = month.Balance - (month.Paid + month.AdvancedPayment);

            // C. Pagamos si tenemos plata
            if (owes > 0 && moneyInHand > 0)
            {
                decimal applied = Math.Min(moneyInHand, owes);
                DateTime rowMonth = DateTime.ParseExact(month.MonthYear, "MM/yyyy", null);
                
                // Si el pago se hace en un mes igual o posterior al de la tabla, es Paid. Sino, Advanced.
                string colToUpdate = (rowMonth > currentRealMonth) ? "advanced_payment" : "paid";
                
                string updPaid = $"UPDATE client_month_balances SET {colToUpdate} = {colToUpdate} + @app WHERE id = @id";
                using var cmdPaid = new SqlCommand(updPaid, connection, transaction);
                cmdPaid.Parameters.AddWithValue("@app", applied);
                cmdPaid.Parameters.AddWithValue("@id", month.Id);
                await cmdPaid.ExecuteNonQueryAsync();

                if (colToUpdate == "paid") month.Paid += applied; else month.AdvancedPayment += applied;
                moneyInHand -= applied;
            }

            // D. La deuda que sobra pasa al mes siguiente
            rolledOverDebt = month.Balance - (month.Paid + month.AdvancedPayment);
        }

        // ==============================================================================
        // --- 4. GENERAMOS EL FUTURO (Adelantos o Proyección de Próximo Pago)
        // ==============================================================================
        string lastMonthStr = existingMonths.Last().MonthYear;
        DateTime lastGeneratedDate = DateTime.ParseExact(lastMonthStr, "MM/yyyy", null);
        decimal lastMonthDebt = rolledOverDebt;
        
        // Solo entramos al bucle si sobra plata O si el último mes quedó en $0 (y necesitamos proyectar 1 mes)
        while (moneyInHand > 0 || lastMonthDebt <= 0)
        {
            lastGeneratedDate = lastGeneratedDate.AddMonths(1);
            string newMonthStr = lastGeneratedDate.ToString("MM/yyyy");

            // 1. Creamos el ticket físico en account_movements
            var culture = new CultureInfo("es-AR");
            string monthName = culture.DateTimeFormat.GetMonthName(lastGeneratedDate.Month);
            string conceptDebit = $"Alquiler {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthName)} {lastGeneratedDate.Year}";

            if (!await accountMovementService.IsDebitAlreadyCreatedAsync(rental.Id, conceptDebit, connection, transaction))
            {
                await accountMovementService.CreateAccountMovementTransactionAsync(new AccountMovement {
                    RentalId = rental.Id, PaymentId = paymentId, MovementDate = dto.Date,
                    MovementType = "DEBITO", Concept = conceptDebit, Amount = currentRent
                }, connection, transaction);
            }

            // 2. Calculamos los saldos de este nuevo mes
            decimal prevBalForThisNewMonth = lastMonthDebt > 0 ? lastMonthDebt : 0m;
            decimal intsForThisNewMonth = 0m; // La mora se limpia al pagar
            decimal totalOwedThisNewMonth = prevBalForThisNewMonth + intsForThisNewMonth + currentRent;

            // 3. Aplicamos la plata
            decimal bucketSize = Math.Max(currentRent, totalOwedThisNewMonth);
            decimal applied = Math.Min(moneyInHand, bucketSize);

            // 4. Insertamos la fila en el Excel
            await _daoMonthBalance.CreateMonthBalanceTransactionAsync(new ClientMonthBalance {
                RentalId = rental.Id,
                MonthYear = newMonthStr,
                PreviousBalance = prevBalForThisNewMonth,
                Interests = intsForThisNewMonth,
                MonthlyDebits = currentRent,
                Paid = 0m,
                AdvancedPayment = applied
            }, connection, transaction);

            moneyInHand -= applied;
            lastMonthDebt = totalOwedThisNewMonth - applied;

            // FRENO DE MANO: Si nos quedamos sin plata, Y el mes que acabamos de crear quedó con deuda, CORTAMOS.
            // Esto evita que se cree "Agosto" cuando "Julio" todavía debe 70.000.
            if (moneyInHand <= 0 && lastMonthDebt > 0) break;
            
            // Si nos quedamos sin plata, pero lastMonthDebt es 0, cortamos también (ya proyectamos el mes vacío)
            if (moneyInHand <= 0 && lastMonthDebt <= 0) break;
        }

        // --- 5. LIMPIEZA DE MORA ---
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
			return await _daoPayment.DeletePaymentTransactionAsync(movementId);
		}

		private decimal RoundToNearest1000(decimal amount)
		{
			if (amount == 0) return 0;
			return Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
		}

	}
}