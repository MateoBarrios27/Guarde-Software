using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dao;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.Payment;
using GuardeSoftwareAPI.Services.accountMovement;

namespace GuardeSoftwareAPI.Services.payment
{

	public class PaymentService : IPaymentService
	{
		private readonly DaoPayment _daoPayment;
		private readonly IAccountMovementService accountMovementService;
		private readonly AccessDB accessDB;

		public PaymentService(AccessDB _accessDB, IAccountMovementService _accountMovementService)
		{
			_daoPayment = new DaoPayment(_accessDB);
			this.accountMovementService = _accountMovementService;
			this.accessDB = _accessDB;
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
					ClientId = row["client_id"] != DBNull.Value ? (int)row["client_id"] : 0
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
			if (payment.Amount <= 0) throw new ArgumentException("Amount must be greater than zero.");
			if (payment.PaymentDate == DateTime.MinValue) throw new ArgumentException("Invalid payment date.");
			return await _daoPayment.CreatePayment(payment);
		}

		public async Task<bool> CreatePaymentWithMovementAsync(CreatePaymentTransaction dto)
		{
			if (dto == null) throw new ArgumentNullException(nameof(dto), "DTO cannot be null.");
			if (dto.ClientId <= 0) throw new ArgumentException("Invalid client ID.");
			if (dto.PaymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");
			if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than 0.");
			if (dto.RentalId <= 0) throw new ArgumentException("Invalid rental ID.");

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
					PaymentDate = DateTime.Now
				};

				int paymentId = await _daoPayment.CreatePaymentTransactionAsync(payment, connection, transaction);

				var movement = new AccountMovement
				{
					RentalId = dto.RentalId,
					PaymentId = paymentId,
					MovementDate = DateTime.Now,
					MovementType = string.IsNullOrWhiteSpace(dto.MovementType) ? "CREDITO" : dto.MovementType,
					Concept = string.IsNullOrWhiteSpace(dto.Concept) ? "Pago de alquiler" : dto.Concept,
					Amount = dto.Amount
				};

				await accountMovementService.CreateAccountMovementTransactionAsync(movement, connection, transaction);

				await transaction.CommitAsync();
				return true;
			}
			catch
			{
				await transaction.RollbackAsync();
				throw;
			}
		}
		
	}
}