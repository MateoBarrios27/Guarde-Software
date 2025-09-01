using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.payment
{

	public interface IPaymentService
	{
		public List<Payment> GetPaymentsList();
		public Payment GetPaymentById(int id);
		public List<Payment> GetPaymentsByClientId(int clientId);
		public bool CreatePayment(Payment payment);
	}
}