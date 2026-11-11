using System.ComponentModel.DataAnnotations;
using GuardeSoftwareAPI.Entities;

namespace GuardeSoftwareAPI.Dtos.MonthlyInterest
{
    public class CreateMonthlyInterestDTO
    {
        /// <summary>Mes de aplicación, en formato 'YYYY-MM'.</summary>
        [Required]
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "El formato debe ser AAAA-MM")]
        public string EffectiveDate { get; set; } = string.Empty;

        [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
        public decimal Percentage { get; set; }

        [Range(2, 28, ErrorMessage = "El día debe estar entre 2 y 28.")]
        public int ApplyDay { get; set; }
    }

    public class UpdateMonthlyInterestDTO
    {
        [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
        public decimal Percentage { get; set; }

        [Range(2, 28, ErrorMessage = "El día debe estar entre 2 y 28.")]
        public int ApplyDay { get; set; }
    }

    /// <summary>
    /// Everything the Settings section and the payment flow need: the defaults that govern a
    /// month without configuration, the configured months and the months already applied.
    /// </summary>
    public class MonthlyInterestOverviewDTO
    {
        public decimal DefaultPercentage { get; set; }
        public int DefaultApplyDay { get; set; }
        /// <summary>Server date the locks were computed with (yyyy-MM-dd).</summary>
        public string Today { get; set; } = string.Empty;
        public List<MonthlyInterestSetting> Settings { get; set; } = [];
        public List<MonthlyInterestRun> Runs { get; set; } = [];
    }
}
