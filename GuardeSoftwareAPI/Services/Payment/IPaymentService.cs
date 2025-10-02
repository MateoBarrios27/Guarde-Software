using System;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Dtos.Payment;

namespace GuardeSoftwareAPI.Services.payment
{

	public interface IPaymentService
	{
		Task<List<Payment>> GetPaymentsList();
		Task<Payment> GetPaymentById(int id);
		Task<List<Payment>> GetPaymentsByClientId(int clientId);
		Task<bool> CreatePayment(Payment payment);

		Task<bool> CreatePaymentWithMovementAsync(CreatePaymentTransaction dto);
	}
}