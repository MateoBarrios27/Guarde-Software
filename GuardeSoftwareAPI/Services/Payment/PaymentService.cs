using System;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Services.payment
{

	public class PaymentService : IPaymentService
    {
		private readonly DaoPayment _daoPayment;
		public PaymentService(AccessDB accessDB)
		{
			_daoPayment = new DaoPayment(accessDB);
		}
		
	}
}