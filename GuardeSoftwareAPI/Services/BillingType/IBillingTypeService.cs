using GuardeSoftwareAPI.Dtos.BillingType;
using GuardeSoftwareAPI.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Services.billingType
{
    public interface IBillingTypeService
    {
        Task<List<BillingType>> GetBillingTypesAsync();
        Task<BillingType> CreateBillingTypeAsync(CreateBillingTypeDTO dto);
        Task<bool> UpdateBillingTypeAsync(int id, UpdateBillingTypeDTO dto);
        Task<bool> DeleteBillingTypeAsync(int id);
    }
}