using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.paymentMethod
{

	public interface IPaymentMethodService
	{
		public List<PaymentMethod> GetPaymentMethodsList();
		public PaymentMethod GetPaymentMethodById(int id);
		public bool DeletePaymentMethod(int paymentMethodId);
		public bool CreatePaymentMethod(PaymentMethod paymentMethod);
	}
}