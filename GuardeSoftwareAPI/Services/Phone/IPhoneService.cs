using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.phone
{
    public interface IPhoneService
    {
        Task<List<Phone>> GetPhonesList();
        Task<List<Phone>> GetPhoneListByClientId(int id);
        Task<Phone> CreatePhoneTransaction(Phone phone, SqlConnection connection, SqlTransaction transaction);
    }
}