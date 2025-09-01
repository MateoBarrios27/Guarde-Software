using System;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using Microsoft.IdentityModel.Tokens;

namespace GuardeSoftwareAPI.Services.paymentMethod
{

	public class PaymentMethodService : IPaymentMethodService
	{
		readonly DaoPaymentMethod _daoPaymentMethod;
		public PaymentMethodService(AccessDB accessDB)
		{
			_daoPaymentMethod = new DaoPaymentMethod(accessDB);
		}

		public List<PaymentMethod> GetPaymentMethodsList()
		{
			DataTable paymentMethodsTable = _daoPaymentMethod.GetPaymentMethods();
			List<PaymentMethod> paymentMethods = new List<PaymentMethod>();

			if (paymentMethodsTable.Rows.Count == 0) throw new ArgumentException("No payments methods found.");

			foreach (DataRow row in paymentMethodsTable.Rows)
			{
				int paymentMethodId = (int)row["payment_method_id"];

				PaymentMethod paymentMethod = new PaymentMethod
				{
					Id = paymentMethodId,
					Name = row["name"]?.ToString() ?? string.Empty
				};

				paymentMethods.Add(paymentMethod);
			}
			return paymentMethods;
		}

		public PaymentMethod GetPaymentMethodById(int paymentMethodId)
		{
			if (paymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");

			DataTable paymentMethodTable = _daoPaymentMethod.GetPaymentMethodById(paymentMethodId);

			if (paymentMethodTable.Rows.Count == 0) throw new ArgumentException("No payment method found with the given ID.");

			DataRow row = paymentMethodTable.Rows[0];

			return new PaymentMethod
			{
				Id = (int)row["payment_method_id"],
				Name = row["name"]?.ToString() ?? string.Empty
			};
		}

		public bool CreatePaymentMethod(PaymentMethod paymentMethod)
		{
			if (paymentMethod == null) throw new ArgumentNullException(nameof(paymentMethod), "Payment method cannot be null.");
			if (string.IsNullOrWhiteSpace(paymentMethod.Name)) throw new ArgumentException("Payment method name cannot be empty.");
			if (_daoPaymentMethod.CreatePaymentMethod(paymentMethod)) return true;
			else return false;
		}

		public bool DeletePaymentMethod(int paymentMethodId)
		{
			if (paymentMethodId <= 0) throw new ArgumentException("Invalid payment method ID.");
			if (_daoPaymentMethod.DeletePaymentMethod(paymentMethodId)) return true;
			else return false;
		}
	}
}