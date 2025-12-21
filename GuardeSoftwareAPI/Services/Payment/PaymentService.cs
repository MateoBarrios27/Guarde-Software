using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Services.accountMovement;
using GuardeSoftwareAPI.Services.rental;

namespace GuardeSoftwareAPI.Services.payment
{

	public class PaymentService : IPaymentService
	{
		private readonly DaoPayment _daoPayment;
		private readonly IAccountMovementService accountMovementService;
		private readonly IRentalService rentalService;
		private readonly ILogger<PaymentService> logger;
		private readonly DaoRental daoRental;
		private readonly AccessDB accessDB;

		public PaymentService(AccessDB _accessDB, IAccountMovementService _accountMovementService, ILogger<PaymentService> logger, IRentalService _rentalService )
		{
			this._daoPayment = new DaoPayment(_accessDB);
			this.accountMovementService = _accountMovementService;
			this.accessDB = _accessDB;
			this.daoRental = new DaoRental(_accessDB);
			this.logger = logger;
			this.rentalService = _rentalService;
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
					ClientName = row["first_name"]?.ToString() ?? string.Empty,
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

		public async Task<bool> CreatePayment(Payment payment)
		{
			if (payment == null) throw new ArgumentNullException(nameof(payment), "Payment cannot be null.");
			if (payment.ClientId <= 0) throw new ArgumentException("Invalid client ID.");
			if (payment.PaymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");
			if (payment.Amount < 0) throw new ArgumentException("Amount must be greater than zero.");
			if (payment.PaymentDate == DateTime.MinValue) throw new ArgumentException("Invalid payment date.");
			return await _daoPayment.CreatePayment(payment);
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
				var payment = new Payment
				{
					ClientId = dto.ClientId,
					PaymentMethodId = dto.PaymentMethodId,
					Amount = dto.Amount,
					// PaymentDate = DateTime.UtcNow // Considera usar DateTime.Now si tu server está en Arg.
					PaymentDate = dto.Date //Cambiamos debido a que se solicito ingresar manualmente fecha de pagos.
				};

				int paymentId = await _daoPayment.CreatePaymentTransactionAsync(payment, connection, transaction);

				//Nueva logica para obtener el rental ID directamente desde el backend 
				var rental = await rentalService.GetRentalByClientIdTransactionAsync(dto.ClientId, connection, transaction);

				if (rental == null) throw new Exception("El cliente no tiene alquiler activo");
				
				//bloqueamos precio si es mayor a 6 meses de pago adelant.
				if (dto.IsAdvancePayment && dto.AdvanceMonths.HasValue && dto.AdvanceMonths.Value > 6)
				{
					DateTime lockEndDate = dto.Date.Date.AddMonths(dto.AdvanceMonths.Value);

					if (!rental.PriceLockEndDate.HasValue || lockEndDate > rental.PriceLockEndDate.Value.Date)
					{
						await daoRental.UpdatePriceLockEndDateTransactionAsync(rental.Id,lockEndDate,connection,transaction);

						rental.PriceLockEndDate = lockEndDate;

						logger.LogInformation(
							"Price lock actualizado para Rental ID {RentalId}. price_lock_end_date = {LockEndDate}",
							rental.Id,
							lockEndDate.ToString("yyyy-MM-dd")
						);
					}
					else
					{
						logger.LogInformation(
							"No se actualiza price lock para Rental ID {RentalId} porque ya existe una fecha mayor o igual ({ExistingLockEndDate}).",
							rental.Id,
							rental.PriceLockEndDate.Value.ToString("yyyy-MM-dd")
						);
					}
				}

				var movement = new AccountMovement
				{
					RentalId = rental.Id,
					PaymentId = paymentId,
					MovementDate = dto.Date, // Igual que arriba
					MovementType = string.IsNullOrWhiteSpace(dto.MovementType) ? "CREDITO" : dto.MovementType,
					Concept = string.IsNullOrWhiteSpace(dto.Concept) ? "Pago de alquiler" : dto.Concept,
					Amount = dto.Amount
				};

				await accountMovementService.CreateAccountMovementTransactionAsync(movement, connection, transaction);

				// --- INICIO DE LA NUEVA LÓGICA ---
				
				// Solo chequeamos si es un "CREDITO" (un pago que reduce deuda)
				if (movement.MovementType == "CREDITO")
				{
					// 1. Obtenemos el balance actualizado DENTRO de la transacción
					decimal newBalance = await daoRental.GetBalanceByRentalIdTransactionAsync(rental.Id, connection, transaction);
					
					logger.LogInformation("Pago de ${Amount} registrado para Rental ID {RentalId}. Nuevo balance: ${NewBalance}", dto.Amount, rental.Id, newBalance);

					// 2. Validamos el balance
					// (Tu lógica de balance es DEBITO - CREDITO, así que > 0 significa que debe)
					if (newBalance <= 0)
					{
						// 3. ¡El cliente saldó su deuda! Reseteamos el contador de mora.
						await daoRental.ResetUnpaidMonthsTransactionAsync(rental.Id, connection, transaction);
						logger.LogInformation("Balance saldado para Rental ID {RentalId}. Contador de meses impagos reseteado a 0.", rental.Id);
					}
					else
					{
						// El cliente pagó, pero sigue debiendo (pago parcial)
						logger.LogInformation("Pago parcial registrado para Rental ID {RentalId}. El cliente aún debe ${NewBalance}. No se resetea el contador de mora.", rental.Id, newBalance);
					}
				}
				
				// --- FIN DE LA NUEVA LÓGICA ---

				await transaction.CommitAsync();
				return true;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				logger.LogError(ex, "Error en CreatePaymentWithMovementAsync. Transacción revertida.");
				throw;
			}
		}

		public async Task<List<DetailedPaymentDto>> GetDetailedPaymentsAsync()
		{
			DataTable table = await _daoPayment.GetDetailedPaymentsAsync();
			List<DetailedPaymentDto> list = new List<DetailedPaymentDto>();

			foreach (DataRow row in table.Rows)
			{
				list.Add(new DetailedPaymentDto
				{
					PaymentId = Convert.ToInt32(row["payment_id"]),
					ClientName = row["client_name"]?.ToString() ?? string.Empty,
					PaymentIdentifier = row["payment_identifier"]?.ToString() ?? string.Empty,
					Amount = Convert.ToDecimal(row["amount"]),
					PaymentDate = Convert.ToDateTime(row["payment_date"]),
					PaymentMethodName = row["payment_method_name"]?.ToString() ?? string.Empty,
					Concept = row["concept"]?.ToString() ?? string.Empty,
					MovementType = row["movement_type"]?.ToString() ?? string.Empty
				});
			}

			return list;
		}

		
	}
}