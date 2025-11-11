using System;
using System.ComponentModel.DataAnnotations;

namespace GuardeSoftwareAPI.Dtos.MonthlyIncrease
{
    public class UpdateMonthlyIncreaseDTO
    {
        [Range(0.01, 999.99, ErrorMessage = "El porcentaje debe ser mayor a 0.")]
        public decimal Percentage { get; set; }
    }
}