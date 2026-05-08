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
			if (dto.PaymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");
			if (dto.Amount < 0) throw new ArgumentException("Amount must be greater than 0.");
			if (dto.Date == DateTime.MinValue) throw new ArgumentException("Invalid date.");
			if (dto.IsAdvancePayment)
			{
				if (!dto.AdvanceMonths.HasValue || dto.AdvanceMonths.Value < 1)
					throw new ArgumentException("AdvanceMonths must be >= 1 when IsAdvancePayment is true.");
			}

			using var connection = accessDB.GetConnectionClose();
			await connection.OpenAsync();
			using var transaction = connection.BeginTransaction();

			try
			{
				// 1. Obtenemos el balance INICIAL antes de registrar nada
				decimal initialBalance = await daoRental.GetBalanceByRentalIdTransactionAsync(dto.ClientId, connection, transaction);

				var payment = new Payment
				{
					ClientId = dto.ClientId,
					PaymentMethodId = dto.PaymentMethodId,
					Amount = dto.Amount,
					PaymentDate = dto.Date 
				};

				int paymentId = await _daoPayment.CreatePaymentTransactionAsync(payment, connection, transaction);

				var rental = await rentalService.GetRentalByClientIdTransactionAsync(dto.ClientId, connection, transaction);
				if (rental == null) throw new Exception("El cliente no tiene alquiler activo");

				bool isPriceLocked = dto.IsAdvancePayment && dto.AdvanceMonths.HasValue && dto.AdvanceMonths.Value >= 6;

				// --- REGLA DE CONGELAMIENTO (6 meses o más) ---
				if (isPriceLocked)
				{
					DateTime lockEndDate = dto.Date.Date.AddMonths(dto.AdvanceMonths.Value);
					if (!rental.PriceLockEndDate.HasValue || lockEndDate > rental.PriceLockEndDate.Value.Date)
					{
						await daoRental.UpdatePriceLockEndDateTransactionAsync(rental.Id, lockEndDate, connection, transaction);
						rental.PriceLockEndDate = lockEndDate;
					}
				}
				// --- REGLA DEL MES SIGUIENTE (Aumento) ---
				else if (dto.IncreasePercentage.HasValue && dto.IncreasePercentage.Value > 0)
				{
					decimal nuevoAbono = (decimal)(rental.CurrentAmount + (rental.CurrentAmount * (dto.IncreasePercentage.Value / 100m)));
					int clientPaymentMethodId = await paymentMethodService.GetPaymentMethodIdByClientId(rental.ClientId);
					
					if (clientPaymentMethodId == 1) nuevoAbono = RoundToNearest1000(nuevoAbono);

					var lastAmountHistory = await rentalAmountHistoryService.GetLatestRentalAmountHistoryTransactionAsync(rental.Id, connection, transaction);
					if (lastAmountHistory != null)
					{
						await rentalAmountHistoryService.EndAndCreateRentalAmountHistoryTransactionAsync(
							lastAmountHistory.Id, rental.Id, nuevoAbono, DateTime.Now, connection, transaction);
					}

					if (rental.IncreaseAnchorDate.HasValue)
					{
						DateTime proximoAumento = rental.IncreaseAnchorDate.Value.AddMonths(3); 
						await rentalService.UpdateIncreaseAnchorDateTransactionAsync(rental.Id, proximoAumento, connection, transaction);
					}
				}

				// --- INSERCIÓN DEL PAGO (CRÉDITO) ---
				var movement = new AccountMovement
				{
					RentalId = rental.Id,
					PaymentId = paymentId, 
					MovementDate = dto.Date,
					MovementType = "CREDITO",
					Concept = string.IsNullOrWhiteSpace(dto.Concept) ? "Pago de alquiler" : dto.Concept,
					Amount = dto.Amount 
				};
				await accountMovementService.CreateAccountMovementTransactionAsync(movement, connection, transaction);

				// --- INSERCIÓN DE DÉBITOS POR MESES ADELANTADOS ---
				if (dto.IsAdvancePayment && dto.AdvanceMonths.HasValue && dto.AdvanceMonths.Value > 0)
				{
					decimal currentRentBase = (decimal)rental.CurrentAmount;
					string[] monthNames = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

					for (int i = 0; i < dto.AdvanceMonths.Value; i++)
					{
						DateTime targetMonthDate = dto.Date.AddMonths(i);
						string monthName = monthNames[targetMonthDate.Month - 1];
						string targetConcept = $"Alquiler {monthName} {targetMonthDate.Year}";

						// Evitamos duplicados si el débito de ese mes ya existía
						if (await accountMovementService.IsDebitAlreadyCreatedAsync(rental.Id, targetConcept, connection, transaction)) continue;

						decimal amountToDebit = 0;
						if (i == 0)
						{
							// Mes actual: Saldamos deuda o alquiler base
							amountToDebit = initialBalance < 0 ? Math.Abs(initialBalance) : currentRentBase;
						}
						else
						{
							amountToDebit = currentRentBase;

							// --- LÓGICA DE INTERÉS ADELANTADO ---
							if (i == 1 && rental.PendingSurcharge > 0)
							{
								var surchargeMovement = new AccountMovement
								{
									RentalId = rental.Id,
									PaymentId = paymentId,
									MovementDate = targetMonthDate, // Fechado en el mes siguiente
									MovementType = "DEBITO",
									Concept = "Recargo por pago fuera de término",
									Amount = (decimal)rental.PendingSurcharge
								};

								await accountMovementService.CreateAccountMovementTransactionAsync(surchargeMovement, connection, transaction);
								logger.LogInformation("Se adelantó el cobro del interés pendiente de ${Amount} para el Rental ID {RentalId}.", rental.PendingSurcharge, rental.Id);

								// Vaciamos la bolsa para que no se duplique
								await daoRental.ResetPendingSurchargeTransactionAsync(rental.Id, connection, transaction);
							}

							// Aumento programado (si no está congelado)
							if (!isPriceLocked && rental.IncreaseAnchorDate.HasValue && dto.IncreasePercentage.HasValue && dto.IncreasePercentage.Value > 0)
							{
								if (new DateTime(targetMonthDate.Year, targetMonthDate.Month, 1) >= 
									new DateTime(rental.IncreaseAnchorDate.Value.Year, rental.IncreaseAnchorDate.Value.Month, 1))
								{
									amountToDebit += currentRentBase * (dto.IncreasePercentage.Value / 100m);
								}
							}

							if (dto.PaymentMethodId == 1) amountToDebit = RoundToNearest1000(amountToDebit);
						}

						var debitMovement = new AccountMovement
						{
							RentalId = rental.Id,
							PaymentId = paymentId,
							MovementDate = new DateTime(targetMonthDate.Year, targetMonthDate.Month, 1),
							MovementType = "DEBITO",
							Concept = targetConcept,
							Amount = amountToDebit
						};

						await accountMovementService.CreateAccountMovementTransactionAsync(debitMovement, connection, transaction);
					}
				}

				// --- COMISIONES / RECARGOS ---
				if (dto.CommissionAmount.HasValue && dto.CommissionAmount.Value != 0)
				{
					var commMovement = new AccountMovement
					{
						RentalId = rental.Id,
						PaymentId = paymentId, 
						MovementDate = dto.Date,
						MovementType = dto.CommissionAmount.Value > 0 ? "DEBITO" : "CREDITO",
						Concept = dto.CommissionConcept ?? "Ajuste por método de pago",
						Amount = Math.Abs(dto.CommissionAmount.Value) 
					};
					await accountMovementService.CreateAccountMovementTransactionAsync(commMovement, connection, transaction);
				}

				// --- REVISIÓN DE MORA ---
				decimal finalBalance = await daoRental.GetBalanceByRentalIdTransactionAsync(rental.Id, connection, transaction);
				if (finalBalance >= 0)
				{
					await daoRental.ResetUnpaidMonthsTransactionAsync(rental.Id, connection, transaction);
				}

				await transaction.CommitAsync();
				return true;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				logger.LogError(ex, "Error en CreatePaymentWithMovementAsync.");
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