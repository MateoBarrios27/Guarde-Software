using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.accountMovement
{

	public interface IAccountMovementService
	{
		List<AccountMovement> GetAccountMovementList();

		List<AccountMovement> GetAccountMovementListByRentalId(int id);

		public bool CreateAccountMovement(AccountMovement accountMovement);
		public Task ApplyMonthlyDebitsAsync();

    }
}