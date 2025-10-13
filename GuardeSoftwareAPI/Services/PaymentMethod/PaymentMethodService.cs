using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.IdentityModel.Tokens;
using GuardeSoftwareAPI.Dtos.PaymentMethod;

namespace GuardeSoftwareAPI.Services.paymentMethod
{

	public class PaymentMethodService : IPaymentMethodService
	{
		readonly DaoPaymentMethod _daoPaymentMethod;
		public PaymentMethodService(AccessDB accessDB)
		{
			_daoPaymentMethod = new DaoPaymentMethod(accessDB);
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

		//don't validate commission, it can be 0 or negative
		public async Task<PaymentMethod> CreatePaymentMethod(PaymentMethod paymentMethod)
		{	
			if (paymentMethod == null) throw new ArgumentNullException(nameof(paymentMethod), "Payment method cannot be null.");
			if (string.IsNullOrWhiteSpace(paymentMethod.Name)) throw new ArgumentException("Payment method name cannot be empty.");
			if (await _daoPaymentMethod.CheckIfPaymentMethodExists(paymentMethod.Name)) throw new ArgumentException("A payment method with the same name already exists.");
			return await _daoPaymentMethod.CreatePaymentMethod(paymentMethod);
		}

		public async Task<bool> DeletePaymentMethod(int paymentMethodId)
		{
			if (paymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");
			if (await _daoPaymentMethod.DeletePaymentMethod(paymentMethodId)) return true;
			else return false;
		}

		public async Task<bool> UpdatePaymentMethod(int paymentMethodId, UpdatePaymentMethodDto dto)
		{
			if (paymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");
			if (dto.Commission < 0m) throw new ArgumentException("Comission cannot be negative.");
			if (await _daoPaymentMethod.UpdatePaymentMethod(paymentMethodId, dto)) return true;
			else return false;
		}
	}
}