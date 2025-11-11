using System.ComponentModel.DataAnnotations;

namespace GuardeSoftwareAPI.Dtos.MonthlyIncrease
{
    public class CreateMonthlyIncreaseDTO
    {
        /// El mes y año de aplicación, en formato 'YYYY-MM'.
        [Required]
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "El formato debe ser AAAA-MM")]
        public string EffectiveDate { get; set; } = string.Empty;

        [Range(0.01, 999.99, ErrorMessage = "El porcentaje debe ser mayor a 0.")]
        public decimal Percentage { get; set; }
    }
}