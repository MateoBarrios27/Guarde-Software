namespace GuardeSoftwareAPI.Utils
{
    /// <summary>
    /// Interest (late-payment surcharge) that governs one calendar month.
    /// ApplyDay is the day the interests job runs; the term to pay without surcharge ends the
    /// day before (CutoffDay). A month without configuration keeps the historical rule:
    /// 10 % applied on the 11th, so the 10th is the last day within term.
    /// </summary>
    public sealed record MonthlyInterestRule(decimal Percentage, int ApplyDay, bool IsDefault)
    {
        public int CutoffDay => ApplyDay - 1;
    }

    /// <summary>
    /// Single source for the interest percentage, the job day and the rounding used by the
    /// interests job (ApplyInterestsJob) and by the surcharge computed when a payment is
    /// registered (LatePaymentSurchargeCalculator). The frontend mirrors it in
    /// core/utils/interest-rule.ts; keep both aligned.
    /// </summary>
    public static class InterestRule
    {
        public const decimal DefaultPercentage = 10m;
        public const int DefaultApplyDay = 11;
        public const int MinApplyDay = 2;
        public const int MaxApplyDay = 28;
        public const decimal MaxPercentage = 100m;

        public static MonthlyInterestRule Default { get; } = new(DefaultPercentage, DefaultApplyDay, true);

        public static DateTime FirstDayOfMonth(DateTime date) => new(date.Year, date.Month, 1);

        /// <summary>
        /// Same rounding as before the configuration existed: the amount is truncated to
        /// whole hundreds. With 10 % the result is identical to the former «base * 0.10m».
        /// </summary>
        public static decimal Calculate(decimal taxableBase, decimal percentage)
        {
            if (taxableBase <= 0m || percentage <= 0m) return 0m;
            decimal amount = taxableBase * percentage / 100m;
            return Math.Floor(amount / 100m) * 100m;
        }

        /// <summary>
        /// A month is closed once its interests were (or should have been) applied:
        /// past months, months already recorded as applied, and the current month from its
        /// apply day on. Closed months cannot be configured, edited or deleted, so the
        /// percentage shown when a payment is registered always matches the one the job used.
        /// </summary>
        public static bool IsMonthClosed(DateTime month, int effectiveApplyDay, DateTime today, bool alreadyApplied)
        {
            if (alreadyApplied) return true;
            var target = FirstDayOfMonth(month);
            var current = FirstDayOfMonth(today);
            if (target < current) return true;
            if (target > current) return false;
            return today.Day >= effectiveApplyDay;
        }
    }
}
