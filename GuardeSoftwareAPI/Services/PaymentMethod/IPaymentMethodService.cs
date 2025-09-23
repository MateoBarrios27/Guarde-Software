using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.paymentMethod
{

	public interface IPaymentMethodService
	{
		Task<List<PaymentMethod>> GetPaymentMethodsList();
		Task<PaymentMethod> GetPaymentMethodById(int id);
		Task<bool> DeletePaymentMethod(int paymentMethodId);
		Task<bool> CreatePaymentMethod(PaymentMethod paymentMethod);
		
	}
}