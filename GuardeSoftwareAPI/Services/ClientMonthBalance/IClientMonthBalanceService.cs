using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.clientMonthBalance
{
    public interface IClientMonthBalanceService
    {
        Task RebuildForRentalAsync(int rentalId);
        Task RebuildForRentalTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction);
        Task RebuildAllActiveRentalsAsync();
    }
}
