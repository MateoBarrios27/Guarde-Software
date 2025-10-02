using System;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Services.accountMovement
{

	public interface IAccountMovementService
	{
		Task<List<AccountMovement>> GetAccountMovementList();

		Task<List<AccountMovement>> GetAccountMovementListByRentalId(int id);

		Task<bool> CreateAccountMovement(AccountMovement accountMovement);
		Task ApplyMonthlyDebitsAsync();

		Task<bool> CreateAccountMovementTransactionAsync(AccountMovement accountMovement, SqlConnection connection, SqlTransaction transaction);

    }
}