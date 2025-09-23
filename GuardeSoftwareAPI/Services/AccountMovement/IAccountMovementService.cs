using System;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Services.accountMovement
{

	public interface IAccountMovementService
	{
		Task<List<AccountMovement>> GetAccountMovementList();

		Task<List<AccountMovement>> GetAccountMovementListByRentalId(int id);

		Task<bool> CreateAccountMovement(AccountMovement accountMovement);
		Task ApplyMonthlyDebitsAsync();

    }
}