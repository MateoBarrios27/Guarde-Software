using System;
using GuardeSoftwareAPI.Dtos.Locker;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Services.locker
{

	public interface ILockerService
	{
		Task<List<Locker>> GetLockersList();

		//Implementar esto
		//List<Locker> GetLockersAvailable();
		Task<List<Locker>> GetLockerListById(int id);

		Task<bool> CreateLocker(Locker locker);

		Task<bool> SetRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction);

		Task<List<GetLockerClientDetailDTO>> GetLockersByClientIdAsync(int clientId);

		Task<bool> DeleteLocker(int id);

		Task<bool> IsLockerAvailableAsync(int lockerId, SqlConnection connection, SqlTransaction transaction);

		Task<bool> UpdateLocker(int lockerId, UpdateLockerDto dto);

		Task<bool> UpdateLockerStatus(int lockerId, UpdateLockerStatusDto dto);
    }
}