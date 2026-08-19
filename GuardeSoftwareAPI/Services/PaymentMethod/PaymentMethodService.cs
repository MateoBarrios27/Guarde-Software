using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.IdentityModel.Tokens;
using GuardeSoftwareAPI.Dtos.PaymentMethod;
using GuardeSoftwareAPI.Services.activityLog;
using System.Text.Json;

namespace GuardeSoftwareAPI.Services.paymentMethod
{

	public class PaymentMethodService : IPaymentMethodService
	{
		readonly DaoPaymentMethod _daoPaymentMethod;
		private readonly IActivityLogService _activityLogService;

		public PaymentMethodService(AccessDB accessDB, IActivityLogService activityLogService)
		{
			_daoPaymentMethod = new DaoPaymentMethod(accessDB);
			_activityLogService = activityLogService;
		}

		public async Task<List<PaymentMethod>> GetPaymentMethodsList()
		{
			DataTable paymentMethodsTable = await _daoPaymentMethod.GetPaymentMethods();
			List<PaymentMethod> paymentMethods = new List<PaymentMethod>();

			if (paymentMethodsTable.Rows.Count == 0) throw new ArgumentException("No payments methods found.");

			foreach (DataRow row in paymentMethodsTable.Rows)
			{
				int paymentMethodId = (int)row["payment_method_id"];

				PaymentMethod paymentMethod = new PaymentMethod
				{
					Id = paymentMethodId,
					Name = row["name"]?.ToString() ?? string.Empty,
					Commission = row["commission"] != DBNull.Value ? (decimal)row["commission"] : 0m
				};

				paymentMethods.Add(paymentMethod);
			}
			return paymentMethods;
		}

		public async Task<PaymentMethod> GetPaymentMethodById(int paymentMethodId)
		{
			if (paymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");

			DataTable paymentMethodTable = await _daoPaymentMethod.GetPaymentMethodById(paymentMethodId);

			if (paymentMethodTable.Rows.Count == 0) throw new ArgumentException("No payment method found with the given ID.");

			DataRow row = paymentMethodTable.Rows[0];

			return new PaymentMethod
			{
				Id = (int)row["payment_method_id"],
				Name = row["name"]?.ToString() ?? string.Empty,
				Commission = row["commission"] != DBNull.Value ? (decimal)row["commission"] : 0m
			};
		}

		public async Task<int> GetPaymentMethodIdByClientId(int clientId)
		{
			if (clientId <= 0) throw new ArgumentException("Invalid client ID.");

			int? paymentMethodId = await _daoPaymentMethod.GetPaymentMethodIdByClientId(clientId);

			if (!paymentMethodId.HasValue) throw new ArgumentException("No payment method found for the given client ID.");

			return paymentMethodId.Value;
		}

		//don't validate commission, it can be 0 or negative
		public async Task<PaymentMethod> CreatePaymentMethod(PaymentMethod paymentMethod)
		{	
			if (paymentMethod == null) throw new ArgumentNullException(nameof(paymentMethod), "Payment method cannot be null.");
			if (string.IsNullOrWhiteSpace(paymentMethod.Name)) throw new ArgumentException("Payment method name cannot be empty.");
			if (await _daoPaymentMethod.CheckIfPaymentMethodExists(paymentMethod.Name)) throw new ArgumentException("A payment method with the same name already exists.");
			PaymentMethod created = await _daoPaymentMethod.CreatePaymentMethod(paymentMethod);
			await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
			{
				Action = "CREATE",
				TableName = "payment_methods",
				RecordId = created.Id,
				NewValue = JsonSerializer.Serialize(new { created.Id, created.Name, created.Commission })
			});
			return created;
		}

		public async Task<bool> DeletePaymentMethod(int paymentMethodId)
		{
			if (paymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");
			PaymentMethod? previous = null;
			try { previous = await GetPaymentMethodById(paymentMethodId); } catch (ArgumentException) { }

			if (await _daoPaymentMethod.DeletePaymentMethod(paymentMethodId))
			{
				await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
				{
					Action = "DELETE",
					TableName = "payment_methods",
					RecordId = paymentMethodId,
					OldValue = previous == null ? null : JsonSerializer.Serialize(new { previous.Id, previous.Name, previous.Commission }),
					NewValue = JsonSerializer.Serialize(new { Active = false })
				});
				return true;
			}
			else return false;
		}

		public async Task<bool> UpdatePaymentMethod(int paymentMethodId, UpdatePaymentMethodDto dto)
		{
			if (paymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");
			if (dto.Name != null && string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Payment method name cannot be empty.");
			if (dto.Commission < 0m) throw new ArgumentException("Comission cannot be negative.");
			PaymentMethod? previous = null;
			try { previous = await GetPaymentMethodById(paymentMethodId); } catch (ArgumentException) { }

			if (await _daoPaymentMethod.UpdatePaymentMethod(paymentMethodId, dto))
			{
				await _activityLogService.TryCreateActivityLogAsync(new ActivityLog
				{
					Action = "UPDATE",
					TableName = "payment_methods",
					RecordId = paymentMethodId,
					OldValue = previous == null ? null : JsonSerializer.Serialize(new { previous.Id, previous.Name, previous.Commission }),
					NewValue = JsonSerializer.Serialize(new { Id = paymentMethodId, dto.Name, dto.Commission })
				});
				return true;
			}
			else return false;
		}
	}
}
