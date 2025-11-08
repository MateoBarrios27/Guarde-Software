using System.ComponentModel.DataAnnotations;

namespace GuardeSoftwareAPI.Dtos.BillingType
{
    public class CreateBillingTypeDTO
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}