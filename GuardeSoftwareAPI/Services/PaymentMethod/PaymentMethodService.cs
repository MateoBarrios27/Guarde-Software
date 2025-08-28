using System;
using GuardeSoftwareAPI.Dao;
using Microsoft.IdentityModel.Tokens;

namespace GuardeSoftwareAPI.Services.paymentMethod
{

	public class PaymentMethodService : IPaymentMethodService
    {
		readonly DaoPaymentMethod daoPaymentMethod;
		public PaymentMethodService(AccessDB accessDB)
		{
			daoPaymentMethod = new DaoPaymentMethod(accessDB);
		}
	}
}