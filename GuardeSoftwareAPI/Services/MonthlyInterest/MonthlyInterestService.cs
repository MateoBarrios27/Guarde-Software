using System.Data;
using System.Globalization;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.MonthlyInterest;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Utils;

namespace GuardeSoftwareAPI.Services.monthlyInterest
{
    public interface IMonthlyInterestService
    {
        Task<MonthlyInterestOverviewDTO> GetOverviewAsync();
        Task<MonthlyInterestSetting> CreateSettingAsync(CreateMonthlyInterestDTO dto);
        Task<bool> UpdateSettingAsync(int id, UpdateMonthlyInterestDTO dto);
        Task<bool> DeleteSettingAsync(int id);
    }

    /// <summary>
    /// Settings «Intereses por mora». A month is editable only until its interests are applied:
    /// once the job ran (or its day arrived) the percentage and the day are fixed, so the job never
    /// runs twice in a month and the amount offered when registering a payment matches the job.
    /// Dates use the server clock, like the job (DateTime.Today).
    /// </summary>
    public class MonthlyInterestService : IMonthlyInterestService
    {
        private readonly DaoMonthlyInterest _dao;
        private readonly ILogger<MonthlyInterestService> _logger;
        private readonly Func<DateTime> _today;

        public MonthlyInterestService(AccessDB accessDB, ILogger<MonthlyInterestService> logger)
            : this(accessDB, logger, () => DateTime.Today) { }

        public MonthlyInterestService(AccessDB accessDB, ILogger<MonthlyInterestService> logger, Func<DateTime> today)
        {
            _dao = new DaoMonthlyInterest(accessDB);
            _logger = logger;
            _today = today;
        }

        public async Task<MonthlyInterestOverviewDTO> GetOverviewAsync()
        {
            DateTime today = _today().Date;
            var runs = new List<MonthlyInterestRun>();
            foreach (DataRow row in (await _dao.GetRecentRunsAsync()).Rows)
                runs.Add(MapRun(row));
            var appliedMonths = runs.Select(run => run.Period).ToHashSet();

            var settings = new List<MonthlyInterestSetting>();
            foreach (DataRow row in (await _dao.GetAllSettingsAsync()).Rows)
            {
                var setting = MapSetting(row);
                setting.IsLocked = InterestRule.IsMonthClosed(setting.EffectiveDate, setting.ApplyDay, today,
                    appliedMonths.Contains(setting.EffectiveDate));
                settings.Add(setting);
            }

            return new MonthlyInterestOverviewDTO
            {
                DefaultPercentage = InterestRule.DefaultPercentage,
                DefaultApplyDay = InterestRule.DefaultApplyDay,
                Today = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Settings = settings,
                Runs = runs,
            };
        }

        public async Task<MonthlyInterestSetting> CreateSettingAsync(CreateMonthlyInterestDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.EffectiveDate))
                throw new ArgumentException("El mes es requerido.");
            if (!DateTime.TryParseExact(dto.EffectiveDate + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime month))
                throw new ArgumentException("Formato de mes inválido. Se esperaba AAAA-MM.");
            ValidateValues(dto.Percentage, dto.ApplyDay);

            DateTime today = _today().Date;
            if (await _dao.IsMonthConfiguredAsync(month))
                throw new InvalidOperationException($"Ya hay intereses configurados para {MonthLabel(month)}. Editá ese mes.");
            await EnsureEditableAsync(month, InterestRule.DefaultApplyDay, today);
            EnsureDayIsAhead(month, dto.ApplyDay, today);

            int id = await _dao.CreateAsync(month, dto.Percentage, dto.ApplyDay);
            _logger.LogInformation("Intereses de {Month}: {Percentage} % el día {Day}.", MonthLabel(month), dto.Percentage, dto.ApplyDay);
            DataRow row = await _dao.GetByIdAsync(id)
                ?? throw new InvalidOperationException("No se pudo recuperar la configuración después de crearla.");
            return MapSetting(row);
        }

        public async Task<bool> UpdateSettingAsync(int id, UpdateMonthlyInterestDTO dto)
        {
            if (id <= 0) throw new ArgumentException("ID inválido.");
            ValidateValues(dto.Percentage, dto.ApplyDay);

            DataRow? row = await _dao.GetByIdAsync(id);
            if (row == null) return false;
            var current = MapSetting(row);
            DateTime today = _today().Date;
            await EnsureEditableAsync(current.EffectiveDate, current.ApplyDay, today);
            EnsureDayIsAhead(current.EffectiveDate, dto.ApplyDay, today);

            _logger.LogInformation("Intereses de {Month} actualizados: {Percentage} % el día {Day}.",
                MonthLabel(current.EffectiveDate), dto.Percentage, dto.ApplyDay);
            return await _dao.UpdateAsync(id, dto.Percentage, dto.ApplyDay);
        }

        public async Task<bool> DeleteSettingAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID inválido.");
            DataRow? row = await _dao.GetByIdAsync(id);
            if (row == null) return false;
            var current = MapSetting(row);
            DateTime today = _today().Date;
            await EnsureEditableAsync(current.EffectiveDate, current.ApplyDay, today);
            // Without the row the month falls back to the default day 11: it must still be ahead.
            if (InterestRule.IsMonthClosed(current.EffectiveDate, InterestRule.DefaultApplyDay, today, false))
                throw new InvalidOperationException(
                    $"No se puede borrar: sin esta configuración {MonthLabel(current.EffectiveDate)} usaría el día {InterestRule.DefaultApplyDay}, que ya pasó. Editá el porcentaje o el día.");

            _logger.LogInformation("Intereses de {Month} eliminados: rige el valor por defecto.", MonthLabel(current.EffectiveDate));
            return await _dao.DeleteAsync(id);
        }

        private async Task EnsureEditableAsync(DateTime month, int effectiveApplyDay, DateTime today)
        {
            if (InterestRule.FirstDayOfMonth(month) < InterestRule.FirstDayOfMonth(today))
                throw new InvalidOperationException($"{MonthLabel(month)} ya pasó: sus intereses no se pueden modificar.");
            if (await _dao.IsMonthAppliedAsync(month))
                throw new InvalidOperationException($"Los intereses de {MonthLabel(month)} ya se aplicaron: no se pueden modificar.");
            if (InterestRule.IsMonthClosed(month, effectiveApplyDay, today, false))
                throw new InvalidOperationException(
                    $"Los intereses de {MonthLabel(month)} ya corresponden (día {effectiveApplyDay}): no se pueden modificar.");
        }

        private static void EnsureDayIsAhead(DateTime month, int applyDay, DateTime today)
        {
            if (InterestRule.FirstDayOfMonth(month) == InterestRule.FirstDayOfMonth(today) && applyDay <= today.Day)
                throw new ArgumentException(
                    $"Para el mes en curso el día debe ser posterior a hoy ({today.Day}).");
        }

        private static void ValidateValues(decimal percentage, int applyDay)
        {
            if (percentage < 0m || percentage > InterestRule.MaxPercentage)
                throw new ArgumentException("El porcentaje debe estar entre 0 y 100.");
            if (decimal.Round(percentage, 2) != percentage)
                throw new ArgumentException("El porcentaje admite hasta dos decimales.");
            if (applyDay < InterestRule.MinApplyDay || applyDay > InterestRule.MaxApplyDay)
                throw new ArgumentException($"El día debe estar entre {InterestRule.MinApplyDay} y {InterestRule.MaxApplyDay}.");
        }

        private static string MonthLabel(DateTime month)
        {
            var culture = new CultureInfo("es-AR");
            string name = culture.DateTimeFormat.GetMonthName(month.Month);
            return $"{culture.TextInfo.ToTitleCase(name)} {month.Year}";
        }

        private static MonthlyInterestSetting MapSetting(DataRow row) => new()
        {
            Id = Convert.ToInt32(row["interest_setting_id"]),
            EffectiveDate = Convert.ToDateTime(row["effective_date"]).Date,
            Percentage = Convert.ToDecimal(row["percentage"]),
            ApplyDay = Convert.ToInt32(row["apply_day"]),
            CreatedAt = Convert.ToDateTime(row["created_at"]),
            UpdatedAt = row["updated_at"] == DBNull.Value ? null : Convert.ToDateTime(row["updated_at"]),
        };

        private static MonthlyInterestRun MapRun(DataRow row) => new()
        {
            Period = Convert.ToDateTime(row["period"]).Date,
            AppliedAt = Convert.ToDateTime(row["applied_at"]),
            Percentage = Convert.ToDecimal(row["percentage"]),
            ApplyDay = Convert.ToInt32(row["apply_day"]),
            UsedDefault = Convert.ToBoolean(row["used_default"]),
            AffectedRentals = Convert.ToInt32(row["affected_rentals"]),
            TotalAmount = Convert.ToDecimal(row["total_amount"]),
        };
    }
}
