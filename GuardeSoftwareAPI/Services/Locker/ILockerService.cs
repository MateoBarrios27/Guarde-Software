using System;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.locker
{

	public interface ILockerService
	{
		List<Locker> GetLockersList();

		//Implementar esto
		//List<Locker> GetLockersAvailable();

		List<Locker> GetLockerListById(int id);

		public bool CreateLocker(Locker locker);

		Task<bool> SetRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction);

    }
}