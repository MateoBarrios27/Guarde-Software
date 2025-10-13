using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.PaymentMethod;

namespace GuardeSoftwareAPI.Services.paymentMethod
{

	public interface IPaymentMethodService
	{
		Task<List<PaymentMethod>> GetPaymentMethodsList();
		Task<PaymentMethod> GetPaymentMethodById(int id);
		Task<PaymentMethod> CreatePaymentMethod(PaymentMethod paymentMethod);
		Task<bool> DeletePaymentMethod(int paymentMethodId);
		Task<bool> UpdatePaymentMethod(int paymentMethodId, UpdatePaymentMethodDto dto);
	}
}